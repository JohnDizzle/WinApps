// SPDX-License-Identifier: GPL-3.0-only
using Microsoft.UI.Reactor;
using Microsoft.UI.Reactor.Core;
using MyApps.Models;
using MyApps.Services.Packages;
using static Microsoft.UI.Reactor.Factories;
using static MyApps.Browser.BrowserUi;

namespace MyApps.Browser;

internal sealed record ApplicationLaunchActionsProps(InstalledApp App, PackageConfiguration? Configuration);
internal sealed class ApplicationLaunchActions : Component<ApplicationLaunchActionsProps>
{
    public override Element Render()
    {
        var busy = UseState(false);
        var message = UseState("");
        var confirmUninstall = UseState(false);
        var removed = UseState(false);
        async void Uninstall()
        {
            if (busy.Value || !confirmUninstall.Value || removed.Value) return;
            confirmUninstall.Set(false);
            busy.Set(true);
            message.Set("Uninstalling application…");
            try
            {
                var result = await new ApplicationUninstaller().UninstallAsync(Props.App);
                removed.Set(result.StartsWith("Application uninstalled", StringComparison.Ordinal));
                message.Set(result);
            }
            finally { busy.Set(false); }
        }
        async void Launch(bool elevated)
        {
            if (busy.Value) return;
            busy.Set(true);
            try { message.Set(await new ApplicationLauncher().LaunchAsync(Props.App, elevated)); }
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
            VStack(8,
                Text($"Uninstall {Props.App.Name} for this Windows user? This removes its package, including other applications in that package, and may delete its local data."),
                HStack(8, Button("Confirm uninstall", Uninstall).IsEnabled(!busy.Value),
                    Button("Cancel", () => confirmUninstall.Set(false)).IsEnabled(!busy.Value)))
                .IsVisible(confirmUninstall.Value).WithKey("uninstall-confirmation"),
            ProgressRing().Set(control => control.IsActive = busy.Value).Width(20).Height(20).IsVisible(busy.Value),
            Text(message.Value).IsVisible(message.Value.Length > 0));
    }
}
