// SPDX-License-Identifier: GPL-3.0-only
using Microsoft.UI.Reactor;
using Microsoft.UI.Reactor.Core;
using WinApps.Models;
using static Microsoft.UI.Reactor.Factories;
using static WinApps.Browser.BrowserUi;

namespace WinApps.Browser;

internal sealed record ApplicationManagementPanelProps(InstalledApp App);

internal sealed class ApplicationManagementPanel : Component<ApplicationManagementPanelProps>
{
    public override Element Render()
    {
        var busy = UseState(false);
        var message = UseState("");
        var noticeVersion = UseState(0);
        void ShowMessage(string text) { noticeVersion.Set(noticeVersion.Value + 1); message.Set(text); }
        var packaged = !string.IsNullOrWhiteSpace(Props.App.FamilyName);
        async void OpenSettings()
        {
            if (busy.Value) return;
            busy.Set(true);
            ShowMessage("");
            try
            {
                var opened = await OpenManagementAsync(Props.App, uri => Windows.System.Launcher.LaunchUriAsync(uri).AsTask());
                ShowMessage(!opened ? "Windows couldn't open app settings. Open Settings → Apps → Installed apps."
                    : packaged ? $"Search Installed apps for {Props.App.Name}, then choose ⋯ → Advanced options where available."
                    : $"Search Installed apps for {Props.App.Name}. Start menu shortcuts may not have an installed-app entry.");
            }
            catch (Exception error) { ShowMessage($"Couldn't open Windows Settings: {error.Message}"); }
            finally { busy.Set(false); }
        }

        void CopyName()
        {
            try
            {
                var data = new Windows.ApplicationModel.DataTransfer.DataPackage();
                data.SetText(Props.App.Name);
                Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(data);
                ShowMessage("App name copied. Paste it into Search apps in Windows Settings.");
            }
            catch (Exception error) { ShowMessage($"Couldn't copy the app name: {error.Message}"); }
        }

        return VStack(4,
            HStack(8,
            Button(HStack(6, FluentGlyph("\uE713"), TextBlock("Installed apps")), OpenSettings)
                .AutomationName($"Open Windows management settings for {Props.App.Name}")
                .IsEnabled(!busy.Value).CornerRadius(9).Padding(10)
                .ToolTip(packaged ? $"Open Installed apps, search for {Props.App.Name}, then choose Advanced options where supported."
                        : "Open Installed apps, then search by name. This shortcut has no package management identity."),
            Button("Copy app name", CopyName).IsEnabled(!busy.Value)
                .AutomationName($"Copy {Props.App.Name} for Installed apps search").CornerRadius(9).Padding(10)),
            Component<TransientActionNotice, TransientActionNoticeProps>(new(message.Value, noticeVersion.Value)).WithKey("management-notice"));
    }
    // The appsfeatures-app query can ignore the target and open the caller's own
    // settings. Never use it to manage another app (MicrosoftDocs/windows-dev-docs#3688).
    internal static Uri ManagementUri(InstalledApp app) => new("ms-settings:appsfeatures");

    internal static Task<bool> OpenManagementAsync(InstalledApp app, Func<Uri, Task<bool>> launch)
        => launch(ManagementUri(app));
}
