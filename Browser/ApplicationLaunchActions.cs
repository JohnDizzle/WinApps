// SPDX-License-Identifier: GPL-3.0-only
using Microsoft.UI.Reactor;
using Microsoft.UI.Reactor.Core;
using WinApps.Models;
using WinApps.Services.Packages;
using static Microsoft.UI.Reactor.Factories;
using static WinApps.Browser.BrowserUi;

namespace WinApps.Browser;

internal sealed record ApplicationLaunchActionsProps(InstalledApp App, PackageConfiguration? Configuration, Action<InstalledApp>? OnUninstalled = null);
internal sealed class ApplicationLaunchActions : Component<ApplicationLaunchActionsProps>
{
    public override Element Render()
    {
        var busy = UseState(false);
        void ShowMessage(string text)
        {
            if (!string.IsNullOrWhiteSpace(text)) StackBehaviorNotification.Queue.Add(text, DateTimeOffset.UtcNow);
        }
        var confirmUninstall = UseState(false);
        var removed = UseState(false);
        var packaged = !string.IsNullOrWhiteSpace(Props.App.FamilyName);
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
                Button(HStack(6, FluentGlyph("\uE768"), TextBlock(Props.App.ConsolePath is null ? "Launch" : "Launch tool")), () => Launch(false))
                    .AutomationName($"Launch {Props.App.Name}").IsEnabled(!busy.Value && !removed.Value && ApplicationLauncher.CanLaunch(Props.App)).CornerRadius(9).Padding(10),
                Component<ApplicationCloseAction, ApplicationCloseActionProps>(new(Props.App)).WithKey($"close-{Props.App.FullName}"),
                Button(HStack(6, FluentGlyph("\uE74D"), TextBlock("Uninstall")), () => confirmUninstall.Set(true))
                    .AutomationName($"Uninstall {Props.App.Name}").IsEnabled(!busy.Value && !removed.Value)
                    .CornerRadius(9).Padding(10).WithKey("uninstall").IsVisible(packaged),
                Button(HStack(6, FluentGlyph("\uE7BA"), TextBlock("Launch elevated")), () => Launch(true))
                        .AutomationName($"Launch {Props.App.Name} as administrator").IsEnabled(!busy.Value && !removed.Value).CornerRadius(9).Padding(10)
                        .IsVisible(ApplicationLauncher.CanLaunchElevated(Props.App) && (!packaged || Props.Configuration?.Launch?.AllowsElevation == true))
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
            Text(busy.Value ? "Working…" : "").IsVisible(busy.Value).WithKey("action-progress"));
    }
}
