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
                    .AutomationName($"Launch {Props.App.Name}").IsEnabled(!busy.Value && Props.App.FamilyName.Length > 0).CornerRadius(9).Padding(10),
                Button(HStack(6, FluentGlyph("\uE7BA"), TextBlock("Launch elevated")), () => Launch(true))
                        .AutomationName($"Launch {Props.App.Name} as administrator").IsEnabled(!busy.Value).CornerRadius(9).Padding(10)
                        .Set(control =>
                        {
                            control.Visibility = Props.Configuration?.Launch?.AllowsElevation == true
                                ? Microsoft.UI.Xaml.Visibility.Visible : Microsoft.UI.Xaml.Visibility.Collapsed;
                            Microsoft.UI.Xaml.Controls.ToolTipService.SetToolTip(control, "Manifest declares allowElevation. Windows will request administrator approval.");
                        }).WithKey("elevated-launch")),
            Text(message.Value));
    }
}
