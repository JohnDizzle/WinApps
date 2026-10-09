// SPDX-License-Identifier: GPL-3.0-only
using System.Diagnostics;
using Microsoft.UI.Xaml;
using Microsoft.UI.Reactor;
using Microsoft.UI.Reactor.Core;
using WinApps.Models;
using WinApps.Services.Storage;
using static Microsoft.UI.Reactor.Factories;

using static WinApps.Browser.BrowserUi;

namespace WinApps.Browser;

internal sealed record ApplicationDetailPaneProps(InstalledApp? Selected, FolderFootprint[] Footprint,
    AppDataInventory? Inventory, AppSettingsSnapshot? Settings, PackageConfiguration? PackageConfiguration,
    AppDataFile? File, string Preview, string View, bool Busy, string Status, Action<string> OnView,
    Action OnMeasure, Action<string> OnOpenFolder, Action<AppDataFile> OnOpenFile, Action<AppDataFile> OnPreviewFile,
    bool Loading = false, bool Compact = false, Action? OnReloadApplication = null,
    Action<InstalledApp>? OnUninstalled = null, string ActionNotice = "", int ActionNoticeVersion = 0,
    InstalledApp[]? Apps = null, Action<InstalledApp>? OnSelect = null, Action? OnClearSelection = null,
    WinApps.Services.Inventory.RunningApplication[]? InitialActivity = null,
    Action<InstalledApp, WinApps.Services.Inventory.RunningApplication[]>? OnSelectActivity = null);

internal sealed class ApplicationDetailPane : Component<ApplicationDetailPaneProps>
{
    private Microsoft.UI.Xaml.Controls.ScrollView? _contentScroll;
    private string? _backSelectionKey;
    private bool _returningHome;
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Microsoft.UI.Xaml.Controls.Button, object> BackButtons = new();

    private void AttachBackButton(Microsoft.UI.Xaml.Controls.Button button)
    {
        void ShowReturning()
        {
            button.IsEnabled = false;
            button.Content = new Microsoft.UI.Xaml.Controls.ProgressRing { IsActive = true, Width = 18, Height = 18 };
            Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(button, "Returning to applications…");
            Microsoft.UI.Xaml.Automation.AutomationProperties.SetItemStatus(button, "Loading");
            Microsoft.UI.Xaml.Controls.ToolTipService.SetToolTip(button, "Returning to applications…");
            BrowserMotion.CardSelectionFeedback(button, true);
        }
        if (_returningHome) ShowReturning();
        else
        {
            Microsoft.UI.Xaml.Controls.ToolTipService.SetToolTip(button, "Back to applications · Clear selection");
            Microsoft.UI.Xaml.Automation.AutomationProperties.SetItemStatus(button, "");
        }
        if (BackButtons.TryGetValue(button, out _)) return;
        BackButtons.Add(button, new object());
        button.Click += (_, _) =>
        {
            if (_returningHome || Props.Selected is null) return;
            _returningHome = true;
            var appKey = Props.Selected.FullName;
            ShowReturning();
            button.DispatcherQueue.TryEnqueue(() =>
            {
                if (Props.Selected?.FullName == appKey) Props.OnClearSelection?.Invoke();
            });
        };
    }

    public override Element Render()
    {
        var selected = Props.Selected;
        if (_backSelectionKey != selected?.FullName)
        {
            _backSelectionKey = selected?.FullName;
            _returningHome = false;
        }
        var padding = Props.Compact ? 12 : 24;
        var layout = UseState("Rows");
        var resourceStatus = UseState("");
        var resourceVersion = UseState(0);
        var activitySummary = UseState("Reading application activity…");
        void ReloadResources()
        {
            resourceStatus.Set("");
            try { UiResources.Reload(); resourceVersion.Set(resourceVersion.Value + 1); resourceStatus.Set("Resources reloaded."); Props.OnReloadApplication?.Invoke(); }
            catch (Exception error) { resourceStatus.Set($"Resources were not changed: {error.Message}"); }
        }
        ButtonElement DocumentationLink(string key)
        {
            var link = UiResources.Current.Links[key];
            return ActionButton(link.Label, () => OpenDocumentation(link.Url));
        }
        void BackToTop() => _contentScroll?.ScrollTo(0, 0, new Microsoft.UI.Xaml.Controls.ScrollingScrollOptions(
            new Windows.UI.ViewManagement.UISettings().AnimationsEnabled
                ? Microsoft.UI.Xaml.Controls.ScrollingAnimationMode.Enabled : Microsoft.UI.Xaml.Controls.ScrollingAnimationMode.Disabled,
            Microsoft.UI.Xaml.Controls.ScrollingSnapPointsMode.Ignore));
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
        Element content = Component<ApplicationActivityPanel, ApplicationActivityPanelProps>(new(OnSummary: value => activitySummary.Set(value),
            Apps: Props.Apps, OnSelect: Props.OnSelect, OnSelectActivity: Props.OnSelectActivity)).WithKey("pc-activity");
        if (selected is not null)
        {
            if (view == "Overview")
                content = VStack(12,
                    Component<ApplicationResourceCard, ApplicationResourceCardProps>(new(selected, Props.InitialActivity)).WithKey($"footprint-{selected.FullName}"),
                    Component<ApplicationOverviewCards, ApplicationOverviewCardsProps>(new(selected, packageConfiguration, Props.Compact, busy, setView, OpenFolder)).WithKey($"overview-cards-{selected.FullName}"));
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
                                Component<PackagePropertyGroups, PackagePropertyGroupsProps>(new(resource.Sections, layout.Value))
                                    .WithKey($"resource-properties-{resource.FullName}"),
                                Disclosure("Complete resource manifest · read only", MarkdownContent.Code(resource.Manifest, "xml", $"Complete manifest for {resource.ResourceId}")),
                                ActionButton("Open installed resource folder", () => OpenFolder(resource.InstallPath)).IsEnabled(!busy),
                                ActionButton("Open resource manifest with default app", () =>
                                    OpenFile(new AppDataFile("Package", "AppxManifest.xml", resource.ManifestPath, 0, DateTime.MinValue, "Resource package manifest")))
                                    .IsEnabled(!busy && resource.Manifest.Length > 0)
                            )).WithKey(resource.FullName)).ToArray())),
                    HStack(8, new[] { "Grid", "Rows", "Widgets" }.Select(option =>
                        ToggleButton(UiResources.LayoutLabel(option),
                            layout.Value == option, enabled => { if (enabled) layout.Set(option); })
                            .Set(BrowserMotion.Enable).Padding(14).CornerRadius(9)
                            .AutomationName($"Package layout {option}").WithKey(option)).ToArray()).Margin(top: 12, bottom: 12),
                    Section("Packaging and UI frameworks",
                        Text("MSIX describes packaging. WinUI 3, UWP, WPF, and Windows Forms describe app technologies. Manifest declarations and dependencies do not always establish the UI framework."),
                        DocumentationLink("Msix"), DocumentationLink("WinUI3"), DocumentationLink("Frameworks")),
                    Component<PackagePropertyGroups, PackagePropertyGroupsProps>(new(packageConfiguration?.Sections ?? [], layout.Value))
                        .WithKey($"package-properties-{selected.FullName}"),
                    Disclosure("Complete manifest · read only",
                        MarkdownContent.Code(packageConfiguration?.Manifest ?? "", "xml", "Complete package manifest")),
                    ActionButton("Open manifest with default app", () =>
                    {
                        if (packageConfiguration is { ManifestPath.Length: > 0 } config)
                            OpenFile(new AppDataFile("Package", "AppxManifest.xml", config.ManifestPath, 0, DateTime.MinValue, "Package manifest"));
                    }).IsEnabled(!busy && packageConfiguration?.Manifest.Length > 0),
                    DocumentationLink("Runtime"));
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
                    FlexRow(TextBlock("File").SemiBold().Flex(grow: 1, basis: 0),
                        TextBlock("Size · Type · Modified").SemiBold().Width(260).Margin(left: 12).Flex(shrink: 0),
                        TextBlock("Open").Width(40).Margin(left: 8).Flex(shrink: 0)).Padding(8).IsVisible(!Props.Compact),
                    VStack(6, files.Select(item => ((Props.Compact ? FlexColumn() : FlexRow()) with { Children = new Element[] {
                        FilePreviewButton(item).Flex(grow: 1, basis: 0),
                        VStack(2, Text($"{item.Bytes:N0} bytes · {item.Kind}"),
                            TextBlock(item.LastWriteUtc.ToLocalTime().ToString("g")).FontSize(12).Foreground(Theme.SecondaryText))
                            .Width(Props.Compact ? double.NaN : 260).Margin(left: Props.Compact ? 0 : 12).Flex(shrink: 0),
                        OpenFileButton(item).Margin(left: 8).Flex(shrink: 0)
                    } }).HAlign(HorizontalAlignment.Stretch).Padding(8).Background(Theme.LayerFill).CornerRadius(8).WithKey(item.FullPath)).ToArray()),
                    VStack(4, (inventory?.Notices ?? Array.Empty<string>()).Select((notice, index) => Text(notice).WithKey($"notice-{index}")).ToArray()),
                    Slot("file-details", file is not null && file.Area == area ? Section("File details", Text(file.FullPath),
                        Text($"Modified: {file.LastWriteUtc.ToLocalTime():g} · {file.Bytes:N0} bytes · {file.Kind}"),
                        ReadOnlyText(preview, "File content preview"))
                        : Text("Select a file to view details. Complete JSON/XML previews are validated and formatted; text previews are limited to 64 KiB.")));
            }
        }

        if (Props.Loading && selected is not null)
            content = Section("Loading application details",
                HStack(16, ProgressRing().Set(control => { control.IsActive = true; control.IsIndeterminate = true; })
                        .AutomationName("Loading application details").Width(36).Height(36),
                    VStack(6, TextBlock($"Loading {selected.Name} · {view}…").SemiBold(),
                        Text("Reading package information, settings, and files. You can select another app while this loads.")))
                    .Set(BrowserMotion.LoadingBounce).Padding(20));

        return
                FlexColumn(
                    Border(VStack(8,
                    (Props.Compact ? FlexColumn() : FlexRow()) with { Children = new Element[] {
                        HStack(12,
                            Slot("back-home", selected is not null && Props.OnClearSelection is not null
                                ? GlassButton(Button(FluentGlyph("\uE72B"), () => { }))
                                    .Width(36).Height(36).Padding(0).CornerRadius(9).AutomationName("Back to applications")
                                    .IsEnabled(!_returningHome)
                                    .Set(AttachBackButton)
                                : Text("").IsVisible(false)),
                            Slot("heading-icon", selected is null ? Icon(FontIcon("\uE71D", "Segoe Fluent Icons")).Width(32).Height(32) : AppIcon(selected, 32)),
                            VStack(4, TextBlock(selected?.Name ?? "WinApps").FontSize(22).SemiBold(),
                                TextBlock(selected is null ? activitySummary.Value :
                                    string.IsNullOrWhiteSpace(selected.FamilyName) ? "Desktop application · This PC" : "Packaged application · This PC")
                                    .FontSize(12).TextWrapping().Foreground(Theme.SecondaryText),
                                TextBlock(status).FontSize(12).TextWrapping().Foreground(Theme.SecondaryText)
                                    .IsVisible(selected is null))).Flex(grow: 1, basis: 0),
                        HStack(8, Slot("app-management", selected is not null ? Component<ApplicationManagementPanel, ApplicationManagementPanelProps>(new(selected))
                            .WithKey($"management-{selected.FullName}") : Text("")).IsEnabled(!Props.Loading),
                        Slot("desktop-terminate", selected is not null && string.IsNullOrWhiteSpace(selected.FamilyName)
                            ? Component<ApplicationTerminateAction, ApplicationTerminateActionProps>(new(selected)).WithKey($"desktop-terminate-{selected.FullName}")
                            : Text("").IsVisible(false)),
                        Slot("launch-actions", selected is not null ? Component<ApplicationLaunchActions, ApplicationLaunchActionsProps>(new(selected, packageConfiguration, Props.OnUninstalled))
                            .WithKey(selected.FullName) : Text(""))
                            .IsEnabled(!Props.Loading).IsVisible(selected is not null && !string.IsNullOrWhiteSpace(selected.FamilyName))).Flex(shrink: 0)
                    } },
                    FlexRow(ScrollView(HStack(8, new[] { "Overview", "Package", "Storage", "Settings", "Registry", "LocalState" }.Select(tab =>
                        GlassButton(Button(tab, () => { if (view != tab) { Props.OnView(tab); BackToTop(); } }), view == tab)
                            .Set(control =>
                            {
                                BrowserMotion.Enable(control);
                                Microsoft.UI.Xaml.Automation.AutomationProperties.SetItemStatus(control, view == tab ? "Selected" : "");
                            }).CornerRadius(9).Padding(10)
                            .AutomationName($"View {tab}").IsEnabled(selected is not null).WithKey(tab)).ToArray()))
                        .Set(control =>
                        {
                            control.ContentOrientation = Microsoft.UI.Xaml.Controls.ScrollingContentOrientation.Horizontal;
                            control.HorizontalScrollMode = Microsoft.UI.Xaml.Controls.ScrollingScrollMode.Enabled;
                            control.HorizontalScrollBarVisibility = Microsoft.UI.Xaml.Controls.ScrollingScrollBarVisibility.Auto;
                            control.VerticalScrollMode = Microsoft.UI.Xaml.Controls.ScrollingScrollMode.Disabled;
                            control.VerticalScrollBarVisibility = Microsoft.UI.Xaml.Controls.ScrollingScrollBarVisibility.Hidden;
                        }).CornerRadius(1).MinWidth(0).Margin(right: 12).Flex(grow: 1, basis: 0),
                    Border(HStack(8,
                    ActionButton("Reload resources", ReloadResources).Padding(10)
                        .Set(control => Microsoft.UI.Xaml.Controls.ToolTipService.SetToolTip(control, UiResources.FilePath)),
                    Button(FluentGlyph("\uE74A"), BackToTop)
                        .AutomationName("Back to top").Padding(10).CornerRadius(9)
                        .Set(control => Microsoft.UI.Xaml.Controls.ToolTipService.SetToolTip(control, "Back to top"))
                        .Background(Theme.LayerFill).WithBorder(Theme.CardStroke)))
                        .Padding(4).VAlign(VerticalAlignment.Top).Flex(shrink: 0)),
                    Component<TransientActionNotice, TransientActionNoticeProps>(new(resourceStatus.Value, resourceVersion.Value)).WithKey("resource-notice")
                    ).Padding(12)).Background(Theme.CardBackground).WithBorder(Theme.CardStroke).CornerRadius(12)
                        .Margin(left: padding, top: 12, right: padding).Flex(shrink: 0),
                ScrollView(VStack(12,
                    VStack(0,
                    TextBlock(status).TextWrapping().FontSize(12).Foreground(Theme.SecondaryText).Margin(left: 4).IsVisible(selected is not null),
                    Component<TransientActionNotice, TransientActionNoticeProps>(new(Props.ActionNotice, Props.ActionNoticeVersion)).WithKey("library-action-notice"))
                        .IsVisible(selected is not null || !string.IsNullOrWhiteSpace(Props.ActionNotice)),
                    VStack(0, content).Set(control => { BrowserMotion.Enable(control); BrowserMotion.SlideEntrance(control); })
                        .WithKey($"content-{selected?.FullName ?? "library"}-{view}-{resourceVersion.Value}")
                ).Padding(left: padding, top: 12, right: padding, bottom: padding)).Set(control => _contentScroll = control).MinWidth(0).Flex(grow: 1, basis: 0)).MinWidth(0).Flex(grow: 1, basis: 0);
    }

    private static void OpenDocumentation(string url) =>
        Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });

    private static Element DetailRow(string label, string value) => FlexRow(
        TextBlock(label).Foreground(Theme.SecondaryText).Width(150).Flex(shrink: 0),
        TextBlock(string.IsNullOrWhiteSpace(value) || value == "Unknown" ? "Not available" : value)
            .TextWrapping().Set(control => control.IsTextSelectionEnabled = true).Margin(left: 16).Flex(grow: 1, basis: 0))
        .Padding(10).Background(Theme.LayerFill).CornerRadius(6);
}
