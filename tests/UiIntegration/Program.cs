// SPDX-License-Identifier: GPL-3.0-only
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Reactor.Core;
using Microsoft.UI.Reactor;
using MyApps.Browser;
using MyApps.Models;
using static Microsoft.UI.Reactor.Factories;

namespace MyApps.UiIntegration;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        Directory.CreateDirectory("artifacts");
        File.WriteAllText("artifacts/ui-checks.log", "Starting native UI checks.\n");
        try
        {
            WinRT.ComWrappersSupport.InitializeComWrappers();
            Application.Start(_ => new ChecksApplication());
        }
        catch (Exception error)
        {
            File.AppendAllText("artifacts/ui-checks.log", error.ToString());
            Environment.ExitCode = 1;
        }
    }
}

public sealed partial class ChecksApplication : Application
{
    private readonly Window _window;
    public ChecksApplication()
    {
        File.AppendAllText("artifacts/ui-checks.log", "Initializing resources.\n");
        InitializeComponent();
        // A hidden, unactivated window keeps the WinUI dispatcher alive for native checks.
        _window = new Window { Content = new Grid() };
        Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread().TryEnqueue(Run);
    }

    private void Run()
    {
        try
        {
            var reconciler = new Reconciler();
            var checks = 0;
            void Check(bool value, string message)
            {
                if (!value) throw new InvalidOperationException(message);
                checks++;
            }
            foreach (var requestedTheme in new[] { ElementTheme.Light, ElementTheme.Dark })
            {
                var frame = WinAppsShell.WindowFrame(requestedTheme, TextBlock("Title"),
                    ErrorBoundary(TextBlock("Content"), _ => TextBlock("Recovery")));
                Check(frame.Modifiers?.Backdrop?.Kind == BackdropKind.Mica,
                    "Mica must be declared on the window root, outside the recovery boundary.");
                var frameNative = (FrameworkElement)reconciler.Mount(frame, () => { })!;
                Check(frameNative.RequestedTheme == requestedTheme,
                    "The window root must own the selected theme for all descendants.");
            }
            var app = new InstalledApp("Fixture", "fixture-family", "fixture-family!App", "Publisher", "1", "", "");
            Element Launch(bool elevated) => Component<ApplicationLaunchActions, ApplicationLaunchActionsProps>(new(app,
                new("", "", "", "", [], "", [], Launch: new("App", "fixture.exe", elevated)))).WithKey("launch");
            var old = Launch(false);
            var native = reconciler.Mount(old, () => { })!;
            foreach (var enabled in new[] { true, false, true })
            {
                var next = Launch(enabled);
                var updated = reconciler.Reconcile(old, next, native, () => { })!;
                Check(ReferenceEquals(native, updated), "Launch action component must stay mounted as configuration arrives.");
                old = next; native = updated;
            }
            Element Slots(bool selected) => VStack(8,
                TextBlock("Stable sibling").WithKey("sibling"),
                BrowserUi.Slot("selection", selected ? Button("Unselect", () => { }) : TextBlock("")),
                BrowserUi.Slot("files", selected ? Expander("App files", TextBlock("Files")) : TextBlock("")));
            old = Slots(false);
            native = reconciler.Mount(old, () => { })!;
            var sibling = ((StackPanel)native).Children[0];
            foreach (var selected in new[] { true, false, true, false })
            {
                var next = Slots(selected);
                native = reconciler.Reconcile(old, next, native, () => { })!;
                Check(ReferenceEquals(((StackPanel)native).Children[0], sibling), "Selection changes must retain unrelated sibling controls.");
                old = next;
            }
            // Exercise actual detail layouts across library, app, and section transitions.
            Element Detail(InstalledApp? selected, string view) => Component<ApplicationDetailPane, ApplicationDetailPaneProps>(
                new(selected, [], null, new([], []), null, null, "", view, false, "", _ => { }, () => { }, _ => { }, _ => { }, _ => { }))
                .WithKey("detail");
            old = Detail(null, "Overview"); native = reconciler.Mount(old, () => { })!;
            foreach (var (selected, view) in new (InstalledApp?, string)[] { (app, "Overview"), (app, "Settings"),
                (app, "LocalState"), (app, "Registry"), (app, "Package"), (app, "Storage"), (null, "Overview") })
            {
                var next = Detail(selected, view);
                var updated = reconciler.Reconcile(old, next, native, () => { })!;
                Check(ReferenceEquals(native, updated), "Detail pane must retain its mounted root across sections.");
                old = next; native = updated;
            }
            // Edit actual native text fields and run the host's next reconciliation pass.
            // Neither an unrelated native row nor its unsaved draft may be replaced.
            var token = new object();
            var first = new AppSetting("LocalSettings", "First", "String", "original", new("LocalSettings", [], "First"));
            var second = new AppSetting("LocalSettings", "Second", "String", "sibling", new("LocalSettings", [], "Second"));
            Element Editors() => VStack(8,
                Component<SettingEditor, SettingEditorProps>(new("fixture", first, token, false)).WithKey("first"),
                Component<SettingEditor, SettingEditorProps>(new("fixture", second, token, false)).WithKey("second"));
            old = Editors(); native = reconciler.Mount(old, () => { })!;
            var firstInput = Descendants(native).OfType<TextBox>().Single(control =>
                Microsoft.UI.Xaml.Automation.AutomationProperties.GetName(control) == "First");
            var siblingInput = Descendants(native).OfType<TextBox>().Single(control =>
                Microsoft.UI.Xaml.Automation.AutomationProperties.GetName(control) == "Second");
            var siblingRow = ((StackPanel)native).Children[1];
            siblingInput.Text = "Unsaved sibling draft";
            var edited = Editors(); native = reconciler.Reconcile(old, edited, native, () => { })!; old = edited;
            firstInput.Text = "Changed first value";
            edited = Editors(); native = reconciler.Reconcile(old, edited, native, () => { })!;
            Check(ReferenceEquals(siblingRow, ((StackPanel)native).Children[1]), "Editing one setting must retain the sibling's mounted row.");
            Check(ReferenceEquals(siblingInput, Descendants(native).OfType<TextBox>().Single(control =>
                Microsoft.UI.Xaml.Automation.AutomationProperties.GetName(control) == "Second")), "Editing one setting must retain the sibling's native input.");
            Check(siblingInput.Text == "Unsaved sibling draft", "Editing one setting must preserve a sibling's unsaved draft.");
            Check(firstInput.Text == "Changed first value", "Edited value must survive reconciliation.");
            var numeric = first with { Type = "Int32", Value = "1048" };
            var boolean = second with { Type = "Boolean", Value = "False" };
            Element TypedEditors() => VStack(8,
                Component<SettingEditor, SettingEditorProps>(new("fixture", numeric, token, false)).WithKey("numeric"),
                Component<SettingEditor, SettingEditorProps>(new("fixture", boolean, token, false)).WithKey("boolean"));
            old = TypedEditors(); native = reconciler.Mount(old, () => { })!;
            var numericInput = Descendants(native).OfType<NumberBox>().Single();
            var booleanInput = Descendants(native).OfType<ToggleSwitch>().Single();
            booleanInput.IsOn = true;
            edited = TypedEditors(); native = reconciler.Reconcile(old, edited, native, () => { })!; old = edited;
            numericInput.Text = "1200.5";
            edited = TypedEditors(); native = reconciler.Reconcile(old, edited, native, () => { })!; old = edited;
            var numericRow = ((StackPanel)native).Children[0];
            Check(!Descendants(numericRow).OfType<Button>().Single(control =>
                Microsoft.UI.Xaml.Automation.AutomationProperties.GetName(control) == "Save").IsEnabled,
                "Fractional integer drafts must disable Save without changing the stored type.");
            numericInput.Text = "1200";
            edited = TypedEditors(); native = reconciler.Reconcile(old, edited, native, () => { })!;
            Check(Descendants(((StackPanel)native).Children[0]).OfType<Button>().Single(control =>
                Microsoft.UI.Xaml.Automation.AutomationProperties.GetName(control) == "Save").IsEnabled,
                "Valid numeric edits must enable Save.");
            Check(ReferenceEquals(numericInput, Descendants(native).OfType<NumberBox>().Single()), "Numeric updates must retain the NumberBox control.");
            Check(ReferenceEquals(booleanInput, Descendants(native).OfType<ToggleSwitch>().Single()) && booleanInput.IsOn,
                "Numeric updates must preserve a sibling Boolean toggle and its unsaved value.");
            var failed = ErrorBoundary(Component<ThrowingView>(), error =>
                Component<ErrorRecoveryPanel, ErrorRecoveryPanelProps>(new("View unavailable", error, () => { }, () => { })));
            native = reconciler.Mount(failed, () => { })!;
            var actions = Descendants(native).OfType<Button>().Select(control =>
                Microsoft.UI.Xaml.Automation.AutomationProperties.GetName(control)).ToArray();
            Check(actions.Contains("Restart WinApps") && actions.Contains("Try again") && actions.Contains("Open Apps List"),
                "A render failure must provide restart, retry, and navigation actions.");
            Check(Descendants(native).OfType<Expander>().Any(control => !control.IsExpanded), "Technical details must start collapsed.");
            Element Boundary(bool broken, int attempt) => VStack(6,
                TextBlock("Unaffected area").WithKey("unaffected"),
                ErrorBoundary(Button("Healthy view", () => { }).WithKey($"view-{attempt}").Set(_ =>
                {
                    if (broken) throw new InvalidCastException("Synthetic native update failure.");
                }), error => Component<ErrorRecoveryPanel, ErrorRecoveryPanelProps>(new("View unavailable", error, () => { }, null)))
                    .WithKey("boundary"));
            old = Boundary(false, 0); native = reconciler.Mount(old, () => { })!;
            var unaffected = ((StackPanel)native).Children[0];
            var brokenView = Boundary(true, 0);
            native = reconciler.Reconcile(old, brokenView, native, () => { })!;
            Check(Descendants(native).OfType<Button>().Any(control =>
                Microsoft.UI.Xaml.Automation.AutomationProperties.GetName(control) == "Restart WinApps"),
                "A native update exception must be contained by the friendly recovery panel.");
            Check(ReferenceEquals(unaffected, ((StackPanel)native).Children[0]), "An area failure must preserve its unaffected sibling.");
            var recoveredView = Boundary(false, 1);
            native = reconciler.Reconcile(brokenView, recoveredView, native, () => { })!;
            Check(!Descendants(native).OfType<Button>().Any(control =>
                Microsoft.UI.Xaml.Automation.AutomationProperties.GetName(control) == "Restart WinApps"),
                "Explicit retry must replace the failed area with a healthy view.");
            Console.WriteLine($"Passed {checks} native UI reconciliation checks.");
            File.AppendAllText("artifacts/ui-checks.log", $"Passed {checks} native UI reconciliation checks.\n");
            Environment.ExitCode = 0;
        }
        catch (Exception error)
        {
            Console.Error.WriteLine(error);
            File.AppendAllText("artifacts/ui-checks.log", error.ToString());
            Environment.ExitCode = 1;
        }
        finally { _window.Close(); Exit(); }
    }

    private static IEnumerable<UIElement> Descendants(UIElement root)
    {
        yield return root;
        IEnumerable<UIElement> children = root switch
        {
            Panel panel => panel.Children,
            Border border when border.Child is not null => [border.Child],
            ContentControl control when control.Content is UIElement child => [child],
            ScrollViewer scroll when scroll.Content is UIElement child => [child],
            ScrollView scroll when scroll.Content is UIElement child => [child],
            _ => []
        };
        foreach (var child in children)
            foreach (var descendant in Descendants(child)) yield return descendant;
    }
}

internal sealed class ThrowingView : Component
{
    public override Element Render() => throw new InvalidOperationException("Synthetic render failure.");
}
