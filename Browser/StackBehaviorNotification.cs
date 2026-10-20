// SPDX-License-Identifier: GPL-3.0-only
using Microsoft.UI.Reactor;
using Microsoft.UI.Reactor.Core;
using Microsoft.UI.Xaml;
using WinApps.Services.Storage;
using static Microsoft.UI.Reactor.Factories;
using static WinApps.Browser.BrowserUi;

namespace WinApps.Browser;

internal sealed class StackBehaviorNotification : Component
{
    // WinApps has one main window. Notices outlive its selected application's components.
    internal static NotificationQueue Queue { get; } = new();

    public override Element Render()
    {
        var entries = UseState(Queue.Entries);
        UseEffect(() =>
        {
            void Update() => entries.Set(Queue.Entries);
            Queue.Changed += Update;
            Update();
            var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
            void Tick(object? sender, object args) => Queue.Expire(DateTimeOffset.UtcNow);
            timer.Tick += Tick;
            timer.Start();
            return () => { Queue.Changed -= Update; timer.Stop(); timer.Tick -= Tick; };
        }, Array.Empty<object>());

        return VStack(8, entries.Value.Select(entry =>
        {
            var attention = entry.Message.Contains("fail", StringComparison.OrdinalIgnoreCase)
                || entry.Message.Contains("could not", StringComparison.OrdinalIgnoreCase)
                || entry.Message.Contains("couldn't", StringComparison.OrdinalIgnoreCase)
                || entry.Message.Contains("no longer", StringComparison.OrdinalIgnoreCase)
                || entry.Message.Contains("registration changed", StringComparison.OrdinalIgnoreCase)
                || entry.Message.Contains("does not declare", StringComparison.OrdinalIgnoreCase)
                || entry.Message.Contains("cannot", StringComparison.OrdinalIgnoreCase)
                || entry.Message.Contains("no package launch", StringComparison.OrdinalIgnoreCase)
                || entry.Message.Contains("unavailable", StringComparison.OrdinalIgnoreCase);
            var cancelled = entry.Message.Contains("cancel", StringComparison.OrdinalIgnoreCase);
            var title = attention ? "Action needs attention" : cancelled ? "Action cancelled" : "Action completed";
            var severity = attention ? Microsoft.UI.Xaml.Controls.InfoBarSeverity.Warning
                : cancelled ? Microsoft.UI.Xaml.Controls.InfoBarSeverity.Informational
                : Microsoft.UI.Xaml.Controls.InfoBarSeverity.Success;
            return InfoBar(title, entry.Message)
                .Severity(severity).IsClosable().Closed(() => Queue.Dismiss(entry.Id))
                .CornerRadius(16)
                .MaxWidth(400).HAlign(HorizontalAlignment.Stretch)
                .WithKey(entry.Id.ToString()).Set(control =>
                {
                    control.IsOpen = true;
                    Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(control, $"{title}: {entry.Message}");
                    Microsoft.UI.Xaml.Automation.AutomationProperties.SetLiveSetting(control,
                        Microsoft.UI.Xaml.Automation.Peers.AutomationLiveSetting.Polite);
                    BrowserMotion.SlideEntrance(control);
                });
        }).ToArray())
            .MaxWidth(400).HAlign(HorizontalAlignment.Right).VAlign(VerticalAlignment.Bottom)
            .IsVisible(entries.Value.Count > 0).AutomationName("Notifications");
    }
}
