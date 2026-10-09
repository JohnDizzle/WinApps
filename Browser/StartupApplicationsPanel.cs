// SPDX-License-Identifier: GPL-3.0-only
using Microsoft.UI.Reactor;
using Microsoft.UI.Reactor.Core;
using WinApps.Services.Inventory;
using static Microsoft.UI.Reactor.Factories;
using static WinApps.Browser.BrowserUi;

namespace WinApps.Browser;

internal sealed class StartupApplicationsPanel : Component
{
    private bool _active;
    private bool _reading;
    public override Element Render()
    {
        var dispatcher = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();
        var snapshot = UseState<StartupSnapshot?>(null);
        var busy = UseState(false);
        var message = UseState("");
        async void Refresh()
        {
            if (!_active || _reading) return;
            _reading = true;
            busy.Set(true);
            try
            {
                var result = await Task.Run(StartupApplications.Read);
                dispatcher.TryEnqueue(() => { if (_active) { snapshot.Set(result); message.Set(""); } });
            }
            catch (Exception error) { dispatcher.TryEnqueue(() => { if (_active) message.Set($"Startup apps unavailable: {error.Message}"); }); }
            finally { dispatcher.TryEnqueue(() => { _reading = false; if (_active) busy.Set(false); }); }
        }
        UseEffect(() =>
        {
            _active = true;
            // Finish mounting the effect before publishing loading state.
            dispatcher.TryEnqueue(Refresh);
            return () => _active = false;
        }, Array.Empty<object>());
        var entries = snapshot.Value?.Apps ?? [];
        return ActivityCardStyle.Section("Startup apps · at sign-in",
            FlexRow(Text(snapshot.Value is null ? "Reading startup apps…" : $"{entries.Length} registered entries · {entries.Count(item => item.State == "Enabled")} enabled · {entries.Count(item => item.State == "Disabled")} disabled")
                    .Flex(grow: 1, basis: 0), ActionButton("Refresh startup apps", Refresh).IsEnabled(!busy.Value)),
            Disclosure("View startup applications", VStack(8,
                Text("Windows Run registrations and startup folders. Packaged startup tasks, services, and scheduled tasks are not included. Status is shown when Windows provides it."),
                VStack(10, entries.Select(item =>
                ActivityCardStyle.Surface(VStack(8,
                    FlexRow(HStack(10, FluentGlyph("\uE7E8", 22), TextBlock(item.Name).SemiBold().TextWrapping()).Flex(grow: 1, basis: 0),
                        item.State == "Enabled" ? ActivityCardStyle.Badge(item.State) : Border(TextBlock(item.State).FontSize(12).SemiBold())
                            .Padding(8, 4).Background(Theme.Ref("ControlFillColorSecondaryBrush")).WithBorder(Theme.CardStroke).CornerRadius(6)),
                    TextBlock(item.Source).TextWrapping().Foreground(Theme.SecondaryText),
                    Disclosure("Startup command or shortcut", ReadOnlyText(item.Command, $"Startup command for {item.Name}")))
                    .Padding(14))
                    .WithKey($"{item.Source}-{item.Name}-{item.Command}")).ToArray()))),
            ActionButton("Open Windows startup settings", async () =>
            {
                try { if (!await Windows.System.Launcher.LaunchUriAsync(new Uri("ms-settings:startupapps"))) message.Set("Windows couldn't open startup settings."); }
                catch (Exception error) { message.Set($"Couldn't open startup settings: {error.Message}"); }
            }).WithKey("open-windows-startup-settings"),
            Text(message.Value).IsVisible(message.Value.Length > 0),
            VStack(4, (snapshot.Value?.Notices ?? []).Select(notice => Text(notice).WithKey(notice)).ToArray()));
    }
}
