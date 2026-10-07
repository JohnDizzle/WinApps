// SPDX-License-Identifier: GPL-3.0-only
using Microsoft.UI.Reactor;
using Microsoft.UI.Reactor.Core;
using MyApps.Models;
using static Microsoft.UI.Reactor.Factories;
using static MyApps.Browser.BrowserUi;

namespace MyApps.Browser;

internal sealed record ApplicationManagementPanelProps(InstalledApp App);

internal sealed class ApplicationManagementPanel : Component<ApplicationManagementPanelProps>
{
    public override Element Render()
    {
        var busy = UseState(false);
        var message = UseState("");
        var packaged = !string.IsNullOrWhiteSpace(Props.App.FamilyName);
        async void OpenSettings()
        {
            if (busy.Value) return;
            busy.Set(true);
            try
            {
                var uri = packaged
                    ? new Uri("ms-settings:appsfeatures-app?" + Uri.EscapeDataString(Props.App.FamilyName))
                    : new Uri("ms-settings:appsfeatures");
                var opened = await Windows.System.Launcher.LaunchUriAsync(uri);
                message.Set(opened ? "Windows Settings opened. After making changes, refresh the application library."
                    : "Windows couldn't open this settings page. Open Settings → Apps → Installed apps.");
            }
            catch (Exception error) { message.Set($"Couldn't open Windows Settings: {error.Message}"); }
            finally { busy.Set(false); }
        }

        return Section("Windows app management",
            Text(packaged ? "Open this application's Windows settings to see its available management actions."
                : "Open Installed apps, then find this application to see its available management actions."),
            Text("Repair · Fix the app while keeping its data, when supported."),
            Text("Reset · Restore initial app state. Windows may delete settings, sign-ins, and local app data."),
            Text("Uninstall · Remove the app. Windows shows availability and confirmation."),
            Button(HStack(6, FluentGlyph("\uE713"), TextBlock(packaged ? "Open app settings in Windows" : "Open Installed apps")), OpenSettings)
                .AutomationName($"Open Windows management settings for {Props.App.Name}")
                .IsEnabled(!busy.Value).CornerRadius(9).Padding(12),
            Text(message.Value));
    }
}
