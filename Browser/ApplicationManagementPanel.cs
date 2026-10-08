// SPDX-License-Identifier: GPL-3.0-only
using Microsoft.UI.Reactor;
using Microsoft.UI.Reactor.Core;
using MyApps.Models;
using System.Diagnostics;
using System.Text.Encodings.Web;
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
                var opened = await OpenManagementAsync(Props.App, uri => Windows.System.Launcher.LaunchUriAsync(uri).AsTask());
                message.Set(!opened ? "Windows couldn't open app settings. Open Settings → Apps → Installed apps."
                    : packaged ? "" : $"Search Installed apps for {Props.App.Name}. Start menu shortcuts may not have an installed-app entry.");
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
    internal static Uri ManagementUri(InstalledApp app) => new ($"ms-settings:appsfeatures");
    internal static async Task<bool> OpenManagementAsync(InstalledApp app, Func<Uri, Task<bool>> launch)
    {
        var target = ManagementUri(app);

        try { if (await launch(target)) return true; }
        catch (Exception) when (!string.IsNullOrWhiteSpace(app.FamilyName)) { }
        if (string.IsNullOrWhiteSpace(app.FamilyName)) return false;
        return await launch(new Uri("ms-settings:appsfeatures"));
    }
}
