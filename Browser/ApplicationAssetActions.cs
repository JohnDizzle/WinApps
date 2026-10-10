// SPDX-License-Identifier: GPL-3.0-only
using Microsoft.UI.Reactor;
using Microsoft.UI.Reactor.Core;
using WinApps.Models;
using WinApps.Services.Packages;
using static Microsoft.UI.Reactor.Factories;
using static WinApps.Browser.BrowserUi;

namespace WinApps.Browser;

internal sealed record ApplicationAssetActionsProps(InstalledApp App);
internal sealed class ApplicationAssetActions : Component<ApplicationAssetActionsProps>
{
    public override Element Render()
    {
        var busy = UseState(false);
        async void Run(Func<Task<string>> action)
        {
            if (busy.Value) return;
            busy.Set(true);
            try { StackBehaviorNotification.Queue.Add(await action(), DateTimeOffset.UtcNow); }
            catch (Exception error) { StackBehaviorNotification.Queue.Add($"Action failed: {error.Message}", DateTimeOffset.UtcNow); }
            finally { busy.Set(false); }
        }
        return ActionButton("Create…", () => { }).IsEnabled(!busy.Value).Set(control =>
        {
            var menu = new Microsoft.UI.Xaml.Controls.MenuFlyout();
            void Add(string label, Func<Task<string>> action)
            {
                var item = new Microsoft.UI.Xaml.Controls.MenuFlyoutItem { Text = label };
                item.Click += (_, _) => Run(action);
                menu.Items.Add(item);
            }
            Add("Desktop shortcut", () => Task.FromResult($"Created: {ApplicationAssets.CreateShortcut(Props.App)}"));
            Add("Pin to taskbar", () => ApplicationAssets.RequestPinAsync(Props.App, true));
            Add("Pin to Start", () => ApplicationAssets.RequestPinAsync(Props.App, false));
            control.Flyout = menu;
        });
    }
}
