// SPDX-License-Identifier: GPL-3.0-only
using Microsoft.UI.Reactor;
using Microsoft.UI.Reactor.Core;
using static Microsoft.UI.Reactor.Factories;
using static WinApps.Browser.BrowserUi;
namespace WinApps.Browser;
internal sealed record ApplicationOverviewDetailsProps(ApplicationOverviewCardsProps Overview, string Section);
internal sealed class ApplicationOverviewDetails : Component<ApplicationOverviewDetailsProps>
{
    public override Element Render()
    {
        var overview = Props.Overview;
        var app = overview.App;
        var package = overview.Package;
        var packaged = !string.IsNullOrWhiteSpace(app.FamilyName);
        Element details = Props.Section switch
        {
            "Package" => ActivityCardStyle.Section("Package information",
                Text(packaged ? $"Application type: {package?.ApplicationType ?? "Unavailable"}" : "Desktop registration or Start menu shortcut. Repair and reset options may not be available."),
                Text($"Trust / AppContainer: {package?.TrustLevel ?? "Not available"}").IsVisible(packaged),
                Text($"UI framework: {package?.UiFramework ?? "Not available"}").IsVisible(packaged),
                Text(package?.ResourceQueryCompleted == true ? $"Related resource packages: {package.ResourcePackages?.Count ?? 0}" : "Resource package discovery unavailable or still loading.").IsVisible(packaged),
                ReadOnlyText(app.FullName, "Application identity"),
                ActionButton("View package manifest and configuration", () => overview.OnView("Package")).IsEnabled(!overview.Busy).IsVisible(packaged)),
            "Files" => ActivityCardStyle.Section("Files and locations",
                Text("Installed folder"), ReadOnlyText(app.InstallPath, "Installed folder"),
                ActionButton("Open installed folder", () => overview.OnOpenFolder(app.InstallPath)).IsEnabled(!string.IsNullOrWhiteSpace(app.InstallPath)),
                Text("App data folder"), ReadOnlyText(string.IsNullOrEmpty(app.DataPath) ? "No app data folder identified" : app.DataPath, "App data folder"),
                ActionButton("Open app data folder", () => overview.OnOpenFolder(app.DataPath)).IsEnabled(!string.IsNullOrWhiteSpace(app.DataPath))),
            _ => ActivityCardStyle.Section("Application information", Text($"Version: {app.Version}"),
                Text($"Publisher: {app.Publisher}"), Text($"Source: {app.Source}"),
                Disclosure("Application identity", ReadOnlyText(app.FullName, "Application identity")))
        };
        return details;
    }
}
