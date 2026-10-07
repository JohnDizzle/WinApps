// SPDX-License-Identifier: GPL-3.0-only
using System.Diagnostics;
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
        void OpenSettings()
        {
            if (busy.Value) return;
            busy.Set(true);
            try
            {
                var uri = ManagementUri(Props.App);
                // This inspector is an unpackaged desktop app. Let the Windows
                // shell activate Settings with the selected family's URI.
                Process.Start(new ProcessStartInfo(uri.OriginalString) { UseShellExecute = true });
                message.Set(packaged ? "" : $"Search Installed apps for {Props.App.Name}. Start menu shortcuts may not have an installed-app entry.");
            }
            catch (Exception error) { message.Set($"Couldn't open Windows Settings: {error.Message}"); }
            finally { busy.Set(false); }
        }

        void CopyName()
        {
            try
            {
                var data = new Windows.ApplicationModel.DataTransfer.DataPackage();
                data.SetText(Props.App.Name);
                Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(data);
                message.Set("App name copied. Paste it into Search apps in Windows Settings.");
            }
            catch (Exception error) { message.Set($"Couldn't copy the app name: {error.Message}"); }
        }

        return VStack(4,
            HStack(8,
            Button(HStack(6, FluentGlyph("\uE713"), TextBlock(packaged ? "Advanced options" : "Installed apps")), OpenSettings)
                .AutomationName($"Open Windows management settings for {Props.App.Name}")
                .IsEnabled(!busy.Value).CornerRadius(9).Padding(10)
                .Set(control => Microsoft.UI.Xaml.Controls.ToolTipService.SetToolTip(control,
                    packaged ? $"Open {Props.App.Name}'s Advanced options in Windows Settings for permissions, repair, reset, and uninstall where supported."
                        : "Open Installed apps, then search by name. This shortcut has no package management identity.")),
            Button("Copy app name", CopyName).IsVisible(!packaged).IsEnabled(!busy.Value)
                .AutomationName($"Copy {Props.App.Name} for Installed apps search").CornerRadius(9).Padding(10)),
            Text(message.Value).IsVisible(message.Value.Length > 0));
    }

    internal static Uri ManagementUri(InstalledApp app) => string.IsNullOrWhiteSpace(app.FamilyName)
        ? new("ms-settings:appsfeatures")
        : new("ms-settings:appsfeatures-app?" + Uri.EscapeDataString(app.FamilyName));
}
