// SPDX-License-Identifier: GPL-3.0-only
using System.Diagnostics;
using Microsoft.UI.Xaml;
using Microsoft.UI.Reactor;
using Microsoft.UI.Reactor.Core;
using MyApps.Models;
using MyApps.Services.Storage;
using static Microsoft.UI.Reactor.Factories;

using static MyApps.Browser.BrowserUi;

namespace MyApps.Browser;

internal sealed record ApplicationDetailPaneProps(InstalledApp? Selected, FolderFootprint[] Footprint,
    AppDataInventory? Inventory, AppSettingsSnapshot? Settings, PackageConfiguration? PackageConfiguration,
    AppDataFile? File, string Preview, string View, bool Busy, string Status, Action<string> OnView,
    Action OnMeasure, Action<string> OnOpenFolder, Action<AppDataFile> OnOpenFile, Action<AppDataFile> OnPreviewFile);

internal sealed class ApplicationDetailPane : Component<ApplicationDetailPaneProps>
{
    public override Element Render()
    {
        var selected = Props.Selected;
        var footprint = Props.Footprint;
        var inventory = Props.Inventory;
        var settings = Props.Settings;
        var packageConfiguration = Props.PackageConfiguration;
        var file = Props.File;
        var preview = Props.Preview;
        var view = Props.View;
        var busy = Props.Busy;
        var status = Props.Status;
        void setView(string next) => Props.OnView(next);
        void Measure() => Props.OnMeasure();
        void OpenFolder(string path) => Props.OnOpenFolder(path);
        void OpenFile(AppDataFile item) => Props.OnOpenFile(item);
        ButtonElement FilePreviewButton(AppDataFile item, bool showDetails = false) => BrowserUi.FilePreviewButton(item, Props.OnPreviewFile, busy, showDetails);
        Element OpenFileButton(AppDataFile item) => BrowserUi.OpenFileButton(item, Props.OnOpenFile, busy);
        Element content = Section("Your library", Text("Browse the application tree, then select an app to explore its settings and stored data."));
        if (selected is not null)
        {
            if (view == "Overview")
                content = Section("About this application", Text($"Version: {selected.Version}"), Text($"Publisher: {selected.Publisher}"),
                    Text($"Source: {selected.Source}"), Text($"Identity: {selected.FullName}"),
                    string.IsNullOrWhiteSpace(selected.FamilyName) ? Text("Desktop application registration") : Section("MSIX configuration",
                        Text($"Application type: {packageConfiguration?.ApplicationType ?? "Loading…"}"),
                        Text($"Trust / AppContainer: {packageConfiguration?.TrustLevel ?? "Loading…"}"),
                        Text($"UI framework: {packageConfiguration?.UiFramework ?? "Loading…"}"),
                        Text(packageConfiguration?.ResourceQueryCompleted == true
                            ? $"Related resource packages: {packageConfiguration.ResourcePackages?.Count ?? 0} installed for this user"
                            : "Related resource packages: discovery unavailable or still loading"),
                        ActionButton("View package manifest and configuration", () => setView("Package")).IsEnabled(!busy)),
                    Text($"Installed location: {(string.IsNullOrEmpty(selected.InstallPath) ? "Unknown" : selected.InstallPath)}"),
                    ActionButton("Open installed folder", () => OpenFolder(selected.InstallPath)).IsEnabled(!string.IsNullOrEmpty(selected.InstallPath)),
                    Text(string.IsNullOrEmpty(selected.DataPath) ? "Persistent data locations require an app-specific adapter." : $"Package data: {selected.DataPath}"));
            else if (view == "Package")
                content = Section("MSIX package · configuration and manifest",
                    Text(packageConfiguration?.ManifestPath ?? "Loading package configuration…"),
                    Text($"Application type: {packageConfiguration?.ApplicationType ?? "Unknown"}"),
                    Text($"Trust / AppContainer: {packageConfiguration?.TrustLevel ?? "Unknown"}"),
                    Text($"UI framework: {packageConfiguration?.UiFramework ?? "Unknown"}"),
                    VStack(8, (packageConfiguration?.Notices ?? Array.Empty<string>()).Select((notice, index) => Text(notice).WithKey($"package-notice-{index}")).ToArray()),
                    Section("Related MSIX resource packages",
                        Text(packageConfiguration?.ResourceQueryCompleted == true
                            ? $"{packageConfiguration.ResourcePackages?.Count ?? 0} installed resource packages share this application's package family."
                            : "Resource package discovery is unavailable or still loading; this does not establish that no resource packs exist."),
                        Text("Language, scale, and other resource declarations appear in each pack's manifest. Installed packs may differ from the selected version; runtime resource selection is not evaluated."),
                        VStack(10, (packageConfiguration?.ResourcePackages ?? Array.Empty<ResourcePackageInfo>()).Select(resource =>
                            Disclosure($"{resource.ResourceId} · {resource.Version} · {resource.Architecture}", VStack(10,
                                ReadOnlyText(resource.Registration, $"Resource package {resource.ResourceId} registration"),
                                Text(resource.ManifestPath),
                                VStack(4, resource.Notices.Select((notice, index) => Text(notice).WithKey($"resource-notice-{index}")).ToArray()),
                                VStack(8, resource.Sections.Select((section, index) => Disclosure(section.Title,
                                    ReadOnlyText(section.Details, $"{resource.ResourceId} {section.Title}"),
                                    isExpanded: section.Title == "Resources and languages").WithKey($"resource-section-{index}")).ToArray()),
                                Disclosure("Complete resource manifest · read only", ReadOnlyText(resource.Manifest, $"Complete manifest for {resource.ResourceId}")),
                                ActionButton("Open installed resource folder", () => OpenFolder(resource.InstallPath)).IsEnabled(!busy),
                                ActionButton("Open resource manifest with default app", () =>
                                    OpenFile(new AppDataFile("Package", "AppxManifest.xml", resource.ManifestPath, 0, DateTime.MinValue, "Resource package manifest")))
                                    .IsEnabled(!busy && resource.Manifest.Length > 0)
                            )).WithKey(resource.FullName)).ToArray())),
                    VStack(10, (packageConfiguration?.Sections ?? Array.Empty<PackageConfigurationSection>()).Select((section, index) =>
                        Disclosure(section.Title,
                            ReadOnlyText(section.Details, section.Title), isExpanded: section.Title == "Selected application")
                            .WithKey($"package-section-{index}")).ToArray()),
                    Disclosure("Complete manifest · read only",
                        ReadOnlyText(packageConfiguration?.Manifest ?? "", "Complete package manifest")),
                    ActionButton("Open manifest with default app", () =>
                    {
                        if (packageConfiguration is { ManifestPath.Length: > 0 } config)
                            OpenFile(new AppDataFile("Package", "AppxManifest.xml", config.ManifestPath, 0, DateTime.MinValue, "Package manifest"));
                    }).IsEnabled(!busy && packageConfiguration?.Manifest.Length > 0),
                    ActionButton("Microsoft Learn · application runtime and trust", () =>
                        Process.Start(new ProcessStartInfo("https://learn.microsoft.com/en-us/uwp/schemas/appxpackage/uapmanifestschema/element-f-application") { UseShellExecute = true })));
            else if (view == "Storage")
                content = Section("Storage footprint", Text(selected.EstimatedSizeKiB is long size ? $"Installer estimate: {size / 1024d:N2} MiB" : "No installer estimate available."),
                    ActionButton("Measure accessible files", Measure).IsEnabled(!busy),
                    VStack(8, footprint.Select(folder => VStack(4, Text(folder.Summary), Text(folder.Path)).WithKey(folder.Path)).ToArray()),
                    Text("Sizes represent logical bytes. Shared install folders may contain files belonging to other applications."));
            else
            {
                var area = view;
                var files = inventory?.Files.Where(item => item.Area == area).ToArray() ?? [];
                var folder = string.IsNullOrEmpty(selected.DataPath) ? "" : AppDataInspector.DataFolder(selected.DataPath, area);
                content = Section(area switch { "Settings" => "Package settings files", "Registry" => "Virtual registry stores", _ => "LocalState · persistent application data" },
                    Text(folder.Length == 0 ? "No known package data directory for this desktop application." : folder),
                    ActionButton("Open folder", () => OpenFolder(folder)).IsEnabled(folder.Length > 0),
                    Text(area switch
                    {
                        "Settings" => "Values below are read through Windows ApplicationData for this package. Stored encrypted strings remain encrypted.",
                        "Registry" => "Per-user virtual registry hives and packaged baselines are separate from ApplicationData settings. Hive keys and values are not decoded or editable yet. A classes hive can contain registrations rather than preferences.",
                        _ => "Explore configuration, databases, logs, images, and other files stored by the application. SQLite headers are identified; database tables are not inspected or editable yet."
                    }),
                    area == "Settings" ? Component<PackageSettingsPanel, PackageSettingsPanelProps>(
                        new(selected.FamilyName, settings, busy)).WithKey($"settings-{selected.FamilyName}") : Text($"{area} file inventory"),
                    Text($"{files.Length} files · {files.Sum(item => item.Bytes) / 1048576d:N2} MiB listed"),
                    VStack(6, files.Select(item => FlexRow(
                        FilePreviewButton(item, showDetails: true).Flex(grow: 1, basis: 0),
                        OpenFileButton(item).Margin(left: 8).Flex(shrink: 0)
                    ).HAlign(HorizontalAlignment.Stretch).WithKey(item.FullPath)).ToArray()),
                    VStack(4, (inventory?.Notices ?? Array.Empty<string>()).Select((notice, index) => Text(notice).WithKey($"notice-{index}")).ToArray()),
                    Slot("file-details", file is not null && file.Area == area ? Section("File details", Text(file.FullPath),
                        Text($"Modified: {file.LastWriteUtc.ToLocalTime():g} · {file.Bytes:N0} bytes · {file.Kind}"),
                        ReadOnlyText(preview, "File content preview"))
                        : Text("Select a file to view details. Complete JSON/XML previews are validated and formatted; text previews are limited to 64 KiB.")));
            }
        }

        if (selected is not null && view == "Overview")
            content = VStack(20, content,
                Component<ApplicationManagementPanel, ApplicationManagementPanelProps>(new(selected))
                    .WithKey($"management-{selected.FullName}"));

        return
                ScrollView(VStack(20,
                    FlexRow(
                        HStack(16, Slot("heading-icon", selected is null ? Icon(FontIcon("\uE71D", "Segoe Fluent Icons")).Width(48).Height(48) : AppIcon(selected, 48)),
                            Heading(selected?.Name ?? "WinApps")).Flex(grow: 1, basis: 0),
                        Slot("launch-actions", selected is not null ? Component<ApplicationLaunchActions, ApplicationLaunchActionsProps>(new(selected, packageConfiguration))
                            .WithKey(selected.FullName) : Text("")).Flex(shrink: 0)),
                    TextBlock(selected?.Publisher ?? "Your installed applications, settings, and storage").TextWrapping().Foreground(Theme.SecondaryText),
                    HStack(8, new[] { "Overview", "Package", "Storage", "Settings", "Registry", "LocalState" }.Select(tab =>
                        ToggleButton(tab, view == tab, enabled => { if (enabled) { Props.OnView(tab); } })
                            .Set(control =>
                            {
                                if (control.Content is Microsoft.UI.Xaml.Controls.StackPanel existing && existing.Tag as string == tab) return;
                                var panel = new Microsoft.UI.Xaml.Controls.StackPanel { Orientation = Microsoft.UI.Xaml.Controls.Orientation.Horizontal, Spacing = 6 };
                                panel.Tag = tab;
                                panel.Children.Add(new Microsoft.UI.Xaml.Controls.FontIcon
                                {
                                    Glyph = tab switch { "Overview" => "\uE946", "Package" => "\uE7B8", "Storage" => "\uEDA2", "Settings" => "\uE713", "Registry" => "\uE8F1", _ => "\uE8B7" },
                                    FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Segoe Fluent Icons"), FontSize = 14
                                });
                                panel.Children.Add(new Microsoft.UI.Xaml.Controls.TextBlock { Text = tab });
                                control.Content = panel;
                            })
                            .CornerRadius(9).Padding(12)
                            .AutomationName($"View {tab}").IsEnabled(selected is not null && !busy).WithKey(tab)).ToArray()),
                    TextBlock(status).TextWrapping().Foreground(Theme.SecondaryText),
                    content.WithKey($"content-{selected?.FullName ?? "library"}-{view}")
                ).Padding(28)).Flex(grow: 1, basis: 0);
    }
}
