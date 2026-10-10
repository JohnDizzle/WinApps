// SPDX-License-Identifier: GPL-3.0-only
using Microsoft.UI.Xaml;
using Microsoft.UI.Reactor;
using Microsoft.UI.Reactor.Core;
using WinApps.Models;
using WinApps.Services.Inventory;
using static Microsoft.UI.Reactor.Factories;
using static WinApps.Browser.BrowserUi;

namespace WinApps.Browser;

internal sealed record ApplicationCloseActionProps(InstalledApp App);
internal sealed class ApplicationCloseAction : Component<ApplicationCloseActionProps>
{
    private RunningApplication[] _processes = [];
    private Action? _refreshView;
    private Microsoft.UI.Xaml.Controls.Button? _closeButton;
    private bool _active;
    private void UpdateProcesses(RunningApplication[] found)
    {
        _processes = found.Where(item => item.Id != Environment.ProcessId && item.Path.Length > 0 && item.WindowHandle != 0).ToArray();
        _refreshView?.Invoke();
    }
    public override Element Render()
    {
        var pending = UseState<RunningApplication?>(null);
        var busy = UseState(false);
        var revision = UseState(0);
        _refreshView = () => revision.Set(revision.Value + 1);
        void Notify(string value)
        {
            if (!string.IsNullOrWhiteSpace(value)) StackBehaviorNotification.Queue.Add(value, DateTimeOffset.UtcNow);
        }
        UseEffect(() =>
        {
            _active = true;
            var dispatcher = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();
            var cancellation = new CancellationTokenSource();
            var token = cancellation.Token;
            var disposed = false;
            var reading = false;
            async void Refresh()
            {
                if (reading) return;
                reading = true;
                try
                {
                    var found = await Task.Run(() => RunningApplications.Read(Props.App, false, token));
                    if (token.IsCancellationRequested) return;
                    dispatcher.TryEnqueue(() => { if (!disposed) UpdateProcesses(found); });
                }
                catch { dispatcher.TryEnqueue(() => { if (!disposed) UpdateProcesses([]); }); }
                finally { reading = false; }
            }
            var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
            void Tick(object? sender, object args) => Refresh();
            timer.Tick += Tick; timer.Start(); Refresh();
            return () => { disposed = true; _active = false; _refreshView = null; timer.Stop(); timer.Tick -= Tick; cancellation.Cancel(); cancellation.Dispose(); };
        }, Props.App.FullName);
        async void CloseApplication(RunningApplication target)
        {
            var dispatcher = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();
            busy.Set(true);
            try
            {
                var result = await Task.Run(() => RunningApplications.RequestClose(target));
                var found = await Task.Run(() => RunningApplications.Read(Props.App, false));
                dispatcher.TryEnqueue(() =>
                {
                    if (!_active) return;
                    Notify(result);
                    UpdateProcesses(found);
                    busy.Set(false);
                });
            }
            catch (Exception error)
            {
                dispatcher.TryEnqueue(() => { if (!_active) return; Notify($"Close request could not complete: {error.Message}"); busy.Set(false); });
            }
        }
        void ChooseWindow()
        {
            if (_processes.Length == 1) { pending.Set(_processes[0]); return; }
            if (_closeButton is null || _processes.Length == 0) return;
            var menu = new Microsoft.UI.Xaml.Controls.MenuFlyout();
            foreach (var process in _processes)
            {
                var item = new Microsoft.UI.Xaml.Controls.MenuFlyoutItem { Text = process.Title };
                item.Click += (_, _) => pending.Set(process);
                menu.Items.Add(item);
            }
            menu.ShowAt(_closeButton);
        }
        return VStack(0,
            Button(HStack(6, FluentGlyph("\uE711"), TextBlock("Close")), ChooseWindow)
                .AutomationName($"Close {Props.App.Name}").CornerRadius(9).Padding(10)
                .IsEnabled(!busy.Value).IsVisible(_processes.Length > 0)
                .Set(control => _closeButton = control),
            (ContentDialog($"Close {Props.App.Name}?", VStack(8,
                Text("Request closing this application window. The application can prompt to save work."),
                Text(pending.Value is { } selected ? $"{selected.Title} · PID {selected.Id}" : "")), "Close application") with
            {
                IsOpen = pending.Value is not null,
                CloseButtonText = "Cancel",
                DefaultButton = Microsoft.UI.Xaml.Controls.ContentDialogButton.Close,
                OnClosed = result =>
                {
                    var target = pending.Value; pending.Set(null);
                    if (result == Microsoft.UI.Xaml.Controls.ContentDialogResult.Primary && target is not null) CloseApplication(target);
                }
            }).WithKey("close-app-confirmation"));
    }
}
