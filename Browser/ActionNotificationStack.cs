// SPDX-License-Identifier: GPL-3.0-only
using Microsoft.UI.Reactor;
using Microsoft.UI.Reactor.Core;
using Microsoft.UI.Xaml;
using WinApps.Services.Storage;
using static Microsoft.UI.Reactor.Factories;
using static WinApps.Browser.BrowserUi;

namespace WinApps.Browser;

internal sealed class ActionNotificationStack : Component
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

        return VStack(6, entries.Value.Select(entry =>
            Border(FlexRow(Text(entry.Message).Flex(grow: 1, basis: 0),
                Button(FluentGlyph("\uE711", 12), () => Queue.Dismiss(entry.Id))
                    .AutomationName("Dismiss notification").Width(32).Height(32).Padding(0)
                    .Margin(left: 8).Flex(shrink: 0)).Padding(10))
                .Background(Theme.LayerFill).WithBorder(Theme.CardStroke).CornerRadius(9)
                .WithKey(entry.Id.ToString()).Set(control =>
                    Microsoft.UI.Xaml.Automation.AutomationProperties.SetLiveSetting(control,
                        Microsoft.UI.Xaml.Automation.Peers.AutomationLiveSetting.Polite))).ToArray())
            .IsVisible(entries.Value.Count > 0).AutomationName("Notifications");
    }
}
