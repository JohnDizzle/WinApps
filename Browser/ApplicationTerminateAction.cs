// SPDX-License-Identifier: GPL-3.0-only
using Microsoft.UI.Xaml;
using Microsoft.UI.Reactor;
using Microsoft.UI.Reactor.Core;
using WinApps.Models;
using WinApps.Services.Inventory;
using static Microsoft.UI.Reactor.Factories;
using static WinApps.Browser.BrowserUi;

namespace WinApps.Browser;

internal sealed record ApplicationTerminateActionProps(InstalledApp App);
internal sealed class ApplicationTerminateAction : Component<ApplicationTerminateActionProps>
{
    private RunningApplication[] _processes = [];
    private Microsoft.UI.Xaml.Controls.Button? _terminateButton;
    private bool _active;
    private void UpdateProcesses(RunningApplication[] found)
    {
        _processes = found.Where(item => item.Id != Environment.ProcessId && item.Path.Length > 0).ToArray();
        if (_terminateButton is not null) _terminateButton.Visibility = _processes.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
    }
    public override Element Render()
    {
        var pending = UseState(Array.Empty<RunningApplication>());
        var message = UseState("");
        var busy = UseState(false);
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
            return () => { disposed = true; _active = false; timer.Stop(); timer.Tick -= Tick; cancellation.Cancel(); cancellation.Dispose(); };
        }, Props.App.FullName);
        async void Terminate(RunningApplication[] targets)
        {
            var dispatcher = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();
            busy.Set(true);
            try
            {
                var results = await Task.Run(() => targets.Select(RunningApplications.Terminate).ToArray());
                var found = await Task.Run(() => RunningApplications.Read(Props.App, false));
                dispatcher.TryEnqueue(() => { if (!_active) return; message.Set(string.Join("\n", results)); UpdateProcesses(found); busy.Set(false); });
            }
            catch (Exception error)
            {
                dispatcher.TryEnqueue(() => { if (!_active) return; message.Set($"Termination could not complete: {error.Message}"); busy.Set(false); });
            }
        }
        return VStack(0,
            Button(HStack(6, FluentGlyph("\uE711"), TextBlock("Terminate")), () => pending.Set(_processes.ToArray()))
                .AutomationName($"Terminate {Props.App.Name}").CornerRadius(9).Padding(10)
                .IsVisible(_processes.Length > 0).IsEnabled(!busy.Value).Set(control => _terminateButton = control),
            (ContentDialog($"Terminate {Props.App.Name}?", VStack(8,
                Text(pending.Value.Length > 1
                    ? $"All {pending.Value.Length} matching processes will stop. Unsaved work may be lost."
                    : "Unsaved work may be lost."),
                Disclosure("Details", VStack(8,
                    Text("Processes are matched by install folder. Shared folders may include other apps."),
                    VStack(4, pending.Value.Select(item => Text($"{System.IO.Path.GetFileName(item.Path)} · PID {item.Id}")
                        .WithKey(item.Id.ToString())).ToArray())))), pending.Value.Length > 1 ? "Terminate all" : "Terminate") with
            {
                IsOpen = pending.Value.Length > 0,
                CloseButtonText = "Cancel",
                DefaultButton = Microsoft.UI.Xaml.Controls.ContentDialogButton.Close,
                OnClosed = result =>
                {
                    var targets = pending.Value; pending.Set([]);
                    if (result == Microsoft.UI.Xaml.Controls.ContentDialogResult.Primary) Terminate(targets);
                }
            }).WithKey("terminate-app-confirmation"),
            Text(message.Value).IsVisible(message.Value.Length > 0));
    }
}
