// SPDX-License-Identifier: GPL-3.0-only
using Microsoft.UI.Reactor;
using Microsoft.UI.Reactor.Core;
using MyApps.Models;
using MyApps.Services.Packages;
using static Microsoft.UI.Reactor.Factories;
using static MyApps.Browser.BrowserUi;

namespace MyApps.Browser;

internal sealed record ApplicationLaunchActionsProps(InstalledApp App, PackageConfiguration? Configuration, Action<InstalledApp>? OnUninstalled = null);
internal sealed class ApplicationLaunchActions : Component<ApplicationLaunchActionsProps>
{
    public override Element Render()
    {
        var busy = UseState(false);
        var message = UseState("");
        var noticeVersion = UseState(0);
        void ShowMessage(string text) { noticeVersion.Set(noticeVersion.Value + 1); message.Set(text); }
        var confirmUninstall = UseState(false);
        var removed = UseState(false);
        async void Uninstall()
        {
            if (busy.Value || removed.Value) return;
            var app = Props.App;
            var onUninstalled = Props.OnUninstalled;
            busy.Set(true);
            ShowMessage("Uninstalling application…");
            try
            {
                var result = await new ApplicationUninstaller().UninstallAsync(app);
                removed.Set(result.Removed);
                ShowMessage(result.Message);
                if (result.Removed) onUninstalled?.Invoke(app);
            }
            catch (Exception error) { ShowMessage($"Uninstall failed: {error.Message}"); }
            finally { busy.Set(false); }
        }
        async void Launch(bool elevated)
        {
            if (busy.Value) return;
            busy.Set(true);
            ShowMessage("");
            try { ShowMessage(await new ApplicationLauncher().LaunchAsync(Props.App, elevated)); }
            finally { busy.Set(false); }
        }
        return VStack(4,
            HStack(8,
                Button(HStack(6, FluentGlyph("\uE768"), TextBlock("Launch")), () => Launch(false))
                    .AutomationName($"Launch {Props.App.Name}").IsEnabled(!busy.Value && !removed.Value && Props.App.FamilyName.Length > 0).CornerRadius(9).Padding(10),
                Button(HStack(6, FluentGlyph("\uE74D"), TextBlock("Uninstall")), () => confirmUninstall.Set(true))
                    .AutomationName($"Uninstall {Props.App.Name}").IsEnabled(!busy.Value && !removed.Value)
                    .CornerRadius(9).Padding(10).WithKey("uninstall"),
                Button(HStack(6, FluentGlyph("\uE7BA"), TextBlock("Launch elevated")), () => Launch(true))
                        .AutomationName($"Launch {Props.App.Name} as administrator").IsEnabled(!busy.Value && !removed.Value).CornerRadius(9).Padding(10)
                        .IsVisible(Props.Configuration?.Launch?.AllowsElevation == true)
                        .Set(control =>
                        {
                            Microsoft.UI.Xaml.Controls.ToolTipService.SetToolTip(control, "Manifest declares allowElevation. Windows will request administrator approval.");
                        }).WithKey("elevated-launch")),
            (ContentDialog($"Uninstall {Props.App.Name}?",
                Text("This removes the package for this Windows user, including other applications in the same package, and may delete its local data."), "Uninstall") with
            {
                IsOpen = confirmUninstall.Value,
                CloseButtonText = "Cancel",
                DefaultButton = Microsoft.UI.Xaml.Controls.ContentDialogButton.Close,
                OnClosed = result =>
                {
                    confirmUninstall.Set(false);
                    if (result == Microsoft.UI.Xaml.Controls.ContentDialogResult.Primary) Uninstall();
                }
            }).WithKey("uninstall-confirmation"),
            ProgressRing().Set(control => control.IsActive = busy.Value).Width(20).Height(20).IsVisible(busy.Value),
            Text(message.Value).IsVisible(busy.Value && message.Value.Length > 0).WithKey("action-progress"),
            Component<TransientActionNotice, TransientActionNoticeProps>(new(busy.Value ? "" : message.Value, noticeVersion.Value))
                .WithKey("action-result"));
    }
}
