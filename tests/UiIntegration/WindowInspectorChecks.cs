// SPDX-License-Identifier: GPL-3.0-only
using System.Diagnostics;
using Windows.Win32;
using Windows.Win32.Foundation;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WinApps.Browser;
using WinApps.Services.Inventory;

namespace MyApps.UiIntegration;

internal static class WindowInspectorChecks
{
    private static void NotifyWinEvent(uint kind, nint window, int objectId, int childId) => PInvoke.NotifyWinEvent(kind, new HWND(window), objectId, childId);

    internal static async Task Run(Window owner)
    {
        void Expect(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
        var handle = WinRT.Interop.WindowNative.GetWindowHandle(owner);
        owner.Title = "Inspector original title";
        using var process = Process.GetCurrentProcess();
        var app = new RunningApplication(process.Id, process.StartTime.ToUniversalTime().Ticks, "Inspector fixture", "Fixture", "fixture.exe", 0, 1, WindowHandle: handle);
        var windows = WindowInspection.Enumerate(process.Id, handle);
        var target = windows.First(item => item.Handle == handle);
        Expect(WindowInspection.Describe(target).Contains($"PID: {process.Id}"), "Inspection must identify the real window owner.");
        Expect(WindowInspection.Describe(target with { ThreadId = 0 }).Contains("reused"), "Stale window identities must not expose another window's details.");
        using (var stream = new WindowEventStream(process.Id, handle))
        {
            await stream.Ready;
            Expect(stream.Error is null, "The dedicated capture thread must start its hooks.");
            NotifyWinEvent(0x800C, handle, 0, 0);
            await Delay(150);
            var original = stream.Drain().First(entry => entry.Window == handle && entry.Name == "Name changed");
            Expect(original.Title == "Inspector original title", "Events must capture the originating window title.");
            owner.Title = "Inspector renamed title";
            NotifyWinEvent(0x800C, handle, 0, 0);
            await Delay(100);
            Expect(stream.Drain().Any(entry => entry.Title == "Inspector renamed title") && original.Title == "Inspector original title",
                "Renaming a window must affect new events without rewriting historical titles.");
            stream.Paused = true;
            NotifyWinEvent(0x800B, handle, 0, 0);
            await Delay(150);
            Expect(stream.Drain().Length == 0, "Paused event monitoring must omit incoming events.");
            stream.Paused = false;
            NotifyWinEvent(0x8004, handle, -4, -100);
            await Delay(150);
            Expect(stream.Drain().Length == 0, "Detailed control-object activity must be omitted by default.");
            stream.IncludeControlObjects = true;
            for (var i = 0; i < 2200; i++) NotifyWinEvent(0x800C, handle, 0, i);
            await Delay(700);
            var batch = stream.Drain();
            Expect(batch.Length > 0 && batch.Length <= WindowEventStream.Capacity, "Native event bursts must remain bounded.");
            Expect(stream.Dropped > 0, "Overflow must be counted rather than silently growing memory.");
            stream.Clear();
            Expect(stream.Dropped == 0 && stream.Drain().Length == 0, "Clear must reset pending events and overflow counters.");
            stream.Dispose();
            await stream.Stopped.WaitAsync(TimeSpan.FromSeconds(3));
            NotifyWinEvent(0x800C, handle, 0, 0);
            await Delay(100);
            Expect(stream.Drain().Length == 0, "Disposed hooks must stop publishing events.");
        }
        using (var excluded = new WindowEventStream(process.Id, handle, handle))
        {
            await excluded.Ready;
            NotifyWinEvent(0x800C, handle, 0, 0);
            await Delay(100);
            Expect(excluded.Drain().Length == 0, "The inspector's own window must be excluded to prevent feedback loops.");
            excluded.Dispose();
            await excluded.Stopped.WaitAsync(TimeSpan.FromSeconds(3));
        }
        var inspector = WindowInspectorWindow.Show(owner, app, activate: false);
        Expect(ReferenceEquals(inspector, WindowInspectorWindow.Show(owner, app, activate: false)), "Repeated inspection must reuse the existing companion.");
        var root = (Grid)inspector.Content;
        root.Measure(new Windows.Foundation.Size(1050, 900));
        root.Arrange(new Windows.Foundation.Rect(0, 0, 1050, 900));
        root.UpdateLayout();
        var list = Descendants(root).OfType<ListView>().Single();
        Expect(list.ItemsSource is not null, "The inspector must bind a persistent live list.");
        Expect(list.SelectionMode == ListViewSelectionMode.None, "Event rows must not show misleading selection checkboxes.");
        Expect(Descendants(root).OfType<CheckBox>().Single(box => (box.Content as string)?.StartsWith("Capture detailed") == true).IsChecked != true,
            "The inspector must default to lower-overhead window capture.");
        var picker = Descendants(root).OfType<ComboBox>().Single(box => box.Header as string is { } label && label.StartsWith("Window to inspect"));
        Expect(picker.Items.Cast<InspectedWindow>().All(window => window.Depth == 0 && window.Visible), "The default window picker must omit hidden windows and child controls.");
        Descendants(root).OfType<CheckBox>().Single(box => (box.Content as string)?.StartsWith("Include child") == true).IsChecked = true;
        Expect(picker.Items.Cast<InspectedWindow>().Any(window => window.Handle == handle), "Advanced inspection must expose the hidden fixture window.");
        await Delay(150);
        NotifyWinEvent(0x800C, handle, 0, 0);
        await Delay(300);
        Expect(list.Items.Cast<WindowEventRow>().Any(row => row.Event == "Name changed" && row.Title == "Inspector renamed title"), "Native events and titles must append to the inspector without rebuilding its list.");
        var filter = Descendants(root).OfType<TextBox>().Single();
        filter.Text = "no-events-match-this-filter";
        await Delay(100);
        Expect(list.Items.Count == 0, "The text filter must update the displayed stream.");
        filter.Text = "";
        await Delay(100);
        Expect(list.Items.Count > 0, "Clearing the filter must restore retained history.");
        var typeFilter = Descendants(root).OfType<ComboBox>().Single(box => box.Header as string == "Event type");
        typeFilter.SelectedItem = "Focus";
        NotifyWinEvent(0x8005, handle, -4, 0);
        await Delay(200);
        Expect(list.Items.Count > 0 && list.Items.Cast<WindowEventRow>().All(row => row.Event == "Focus"), "The explicit event-type filter must show only matching events.");
        typeFilter.SelectedIndex = 0;
        await Delay(100);
        root.Measure(new Windows.Foundation.Size(1050, 900));
        root.Arrange(new Windows.Foundation.Rect(0, 0, 1050, 900));
        var firstRow = (WindowEventRow)list.Items[0];
        list.ScrollIntoView(firstRow);
        root.UpdateLayout();
        var realized = list.ContainerFromIndex(0) as ListViewItem;
        Expect(realized is not null && VisualDescendants(realized).OfType<TextBlock>().Any(block => block.Text == firstRow.Title),
            "The event table must render the bound window title in its row template.");
        var pause = Descendants(root).OfType<Button>().Single(button => button.Content as string == "Pause");
        ((Microsoft.UI.Xaml.Automation.Provider.IInvokeProvider)new Microsoft.UI.Xaml.Automation.Peers.ButtonAutomationPeer(pause)
            .GetPattern(Microsoft.UI.Xaml.Automation.Peers.PatternInterface.Invoke)).Invoke();
        await Delay(100);
        Expect(pause.Content as string == "Resume", "Pause must offer a resume action.");
        var count = list.Items.Count;
        NotifyWinEvent(0x800C, handle, 0, 0);
        await Delay(200);
        Expect(list.Items.Count == count, "A paused inspector must stop appending target events.");
        inspector.Close();
        var reopened = WindowInspectorWindow.Show(owner, app, activate: false);
        Expect(!ReferenceEquals(inspector, reopened), "Closing must release the inspector session so it can reopen cleanly.");
        reopened.Close();
        var stale = WindowInspectorWindow.Show(owner, app with { Started = 1 }, activate: false);
        Expect(((Grid)stale.Content).Children.OfType<TextBlock>().Any(block => block.Text.Contains("original process")), "A stale live card must not attach to a reused PID.");
        stale.Close();
        Console.WriteLine("Passed window identity, captured titles, event table binding, default/advanced windows, filters, pause, bounded overflow and inspector lifecycle checks.");
    }

    private static IEnumerable<UIElement> Descendants(UIElement root)
    {
        yield return root;
        IEnumerable<UIElement> children = root switch
        {
            Panel panel => panel.Children,
            Border { Child: { } child } => [child],
            ScrollViewer { Content: UIElement child } => [child],
            ContentControl { Content: UIElement child } => [child],
            _ => []
        };
        foreach (var child in children)
            foreach (var descendant in Descendants(child)) yield return descendant;
    }

    private static IEnumerable<DependencyObject> VisualDescendants(DependencyObject root)
    {
        yield return root;
        for (var i = 0; i < Microsoft.UI.Xaml.Media.VisualTreeHelper.GetChildrenCount(root); i++)
            foreach (var child in VisualDescendants(Microsoft.UI.Xaml.Media.VisualTreeHelper.GetChild(root, i))) yield return child;
    }

    private static Task Delay(int milliseconds)
    {
        var completion = new TaskCompletionSource();
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(milliseconds) };
        timer.Tick += Tick;
        void Tick(object? sender, object args) { timer.Stop(); timer.Tick -= Tick; completion.SetResult(); }
        timer.Start();
        return completion.Task;
    }
}
