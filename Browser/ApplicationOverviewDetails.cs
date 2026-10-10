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
        Element ShortcutDetails() => VStack(8,
            Text("Shortcut"), ReadOnlyText(app.LaunchPath ?? "", "Shortcut path"),
            Text("Executable target"), ReadOnlyText(app.ExecutablePath ?? "Unavailable", "Executable target"),
            Text("Arguments"), ReadOnlyText(string.IsNullOrWhiteSpace(app.LaunchArguments) ? "None" : app.LaunchArguments, "Shortcut arguments"),
            Text("Start in · working directory"), ReadOnlyText(string.IsNullOrWhiteSpace(app.WorkingDirectory)
                ? "Not set in shortcut" : app.WorkingDirectory, "Shortcut working directory"))
            .IsVisible(app.LaunchPath is not null);
        Element TargetFiles()
        {
            var folder = app.ExecutablePath is not null ? System.IO.Path.GetDirectoryName(app.ExecutablePath) : null;
            if (folder is null) return Text("").IsVisible(false);
            try
            {
                var entries = System.IO.Directory.EnumerateFileSystemEntries(folder).OrderBy(path => path, StringComparer.OrdinalIgnoreCase).Take(201).ToArray();
                return Disclosure("Target directory files", VStack(6,
                    new[] { ReadOnlyText(folder, "Target directory"),
                        ActionButton("Open target directory", () => overview.OnOpenFolder(folder)) }
                    .Concat(entries.Take(200).Select(path => ReadOnlyText(System.IO.Path.GetFileName(path), "Target directory entry")))
                    .Append(Text("Showing the first 200 entries. Open the directory to see all files.").IsVisible(entries.Length > 200)).ToArray()));
            }
            catch (Exception error) when (error is System.IO.IOException or UnauthorizedAccessException)
            { return Text($"Target directory unavailable: {error.Message}"); }
        }
        Element details = Props.Section switch
        {
            "Package" => ActivityCardStyle.SectionWithAction("Package information",
                Component<ApplicationAssetActions, ApplicationAssetActionsProps>(new(app)).WithKey($"assets-{app.FullName}"),
                Text(packaged ? $"Application type: {package?.ApplicationType ?? "Unavailable"}" : "Desktop registration or Start menu shortcut. Repair and reset options may not be available."),
                Text($"Trust / AppContainer: {package?.TrustLevel ?? "Not available"}").IsVisible(packaged),
                Text($"UI framework: {package?.UiFramework ?? "Not available"}").IsVisible(packaged),
                Text(package?.ResourceQueryCompleted == true ? $"Related resource packages: {package.ResourcePackages?.Count ?? 0}" : "Resource package discovery unavailable or still loading.").IsVisible(packaged),
                ReadOnlyText(app.FullName, "Application identity"),
                ShortcutDetails(),
                ActionButton("View package manifest and configuration", () => overview.OnView("Package")).IsEnabled(!overview.Busy).IsVisible(packaged)),
            "Files" => ActivityCardStyle.Section("Files and locations",
                ShortcutDetails(), TargetFiles(),
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
