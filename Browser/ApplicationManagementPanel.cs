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
                message.Set(opened ? ""
                    : "Windows couldn't open this settings page. Open Settings → Apps → Installed apps.");
            }
            catch (Exception error) { message.Set($"Couldn't open Windows Settings: {error.Message}"); }
            finally { busy.Set(false); }
        }

        return VStack(4,
            Button(HStack(6, FluentGlyph("\uE713"), TextBlock("App management")), OpenSettings)
                .AutomationName($"Open Windows management settings for {Props.App.Name}")
                .IsEnabled(!busy.Value).CornerRadius(9).Padding(10)
                .Set(control => Microsoft.UI.Xaml.Controls.ToolTipService.SetToolTip(control,
                    packaged ? "Open Windows app settings for repair, reset, and uninstall where supported."
                        : "Open Windows Installed apps to manage this desktop application.")),
            Text(message.Value));
    }
}
