// SPDX-License-Identifier: GPL-3.0-only
using Microsoft.UI.Xaml;
using Microsoft.UI.Reactor;
using Microsoft.UI.Reactor.Core;
using MyApps.Models;
using static Microsoft.UI.Reactor.Factories;

using static MyApps.Browser.BrowserUi;

namespace MyApps.Browser;

internal sealed record ApplicationRailProps(InstalledApp[] Apps, InstalledApp? Selected, AppDataInventory? Inventory,
    string View, bool Busy, Action<InstalledApp> OnSelect, Action OnUnselect, Action OnRefresh,
    Action<AppDataFile> OnPreviewFile, Action<AppDataFile> OnOpenFile, Action<string> OnView, bool Loading = false, bool Compact = false);

internal sealed class ApplicationRail : Component<ApplicationRailProps>
{
    private Microsoft.UI.Xaml.Controls.ScrollView? _railScroll;
    private bool _resetScroll;
    public override Element Render()
    {
        var letter = UseState("All");
        var search = UseState("");
        var appsListOpen = UseState(true);
        var appFilesOpen = UseState(false);
        var expandedAreas = UseState(Array.Empty<string>());
        var apps = Props.Apps;
        var selected = Props.Selected;
        var inventory = Props.Inventory;
        var busy = Props.Busy;
        var filtering = !string.IsNullOrWhiteSpace(search.Value);
        void Filter(string value)
        {
            search.Set(value);
            letter.Set("All");
            appsListOpen.Set(string.IsNullOrWhiteSpace(value));
        }
        UseEffect(() =>
        {
            appsListOpen.Set(Props.Selected is null);
            appFilesOpen.Set(Props.Selected is not null && Props.Inventory is not null);
            expandedAreas.Set(Array.Empty<string>());
        }, Props.Selected?.FullName);
        UseEffect(() =>
        {
            if (Props.Selected is not null && Props.Inventory is not null) appFilesOpen.Set(true);
        }, Props.Selected?.FullName, Props.Inventory is not null);
        void Select(AppTreeNode node) { if (node.App is not null) Props.OnSelect(node.App); }
        void Unselect() => Props.OnUnselect();
        void Scan()
        {
            _resetScroll = true;
            letter.Set("All"); search.Set(""); appsListOpen.Set(true); appFilesOpen.Set(false);
            Props.OnRefresh();
        }
        ButtonElement FilePreviewButton(AppDataFile item, bool showDetails = false) => BrowserUi.FilePreviewButton(item, Props.OnPreviewFile, busy, showDetails);
        Element OpenFileButton(AppDataFile item) => BrowserUi.OpenFileButton(item, Props.OnOpenFile, busy);
        var matches = apps.Where(app => app.Name.Contains(search.Value, StringComparison.OrdinalIgnoreCase)
            || app.Publisher.Contains(search.Value, StringComparison.OrdinalIgnoreCase)
            || app.FamilyName.Contains(search.Value, StringComparison.OrdinalIgnoreCase)).ToArray();
        string Initial(InstalledApp app)
        {
            var name = app.Name.TrimStart();
            if (name.Length == 0) return "#";
            var first = char.ToUpperInvariant(name[0]);
            return first is >= 'A' and <= 'Z' ? first.ToString() : "#";
        }
        var visibleApps = matches.Where(app => filtering || letter.Value == "All" || Initial(app) == letter.Value)
            .OrderBy(app => app.Name, StringComparer.OrdinalIgnoreCase).ToArray();
        var letters = new[] { "All", "#" }.Concat(Enumerable.Range('A', 26).Select(code => ((char)code).ToString())).ToArray();
        var keyboard = ScrollView(VStack(6, letters.Chunk(4).Select((row, index) => HStack(6, row.Select(key =>
            ToggleButton(key, letter.Value == key, enabled => { if (enabled) letter.Set(key); })
                .Width(64).Height(40).Padding(0).AutomationName($"Show {key} applications")
                .CornerRadius(8)
                .IsEnabled(key == "All" || matches.Any(app => Initial(app) == key)).WithKey(key)).ToArray())
                .WithKey($"letters-{index}")).ToArray()).Width(274))
            .Set(control =>
            {
                control.ContentOrientation = Microsoft.UI.Xaml.Controls.ScrollingContentOrientation.Horizontal;
                control.HorizontalScrollMode = Microsoft.UI.Xaml.Controls.ScrollingScrollMode.Enabled;
                control.HorizontalScrollBarVisibility = Microsoft.UI.Xaml.Controls.ScrollingScrollBarVisibility.Auto;
                control.VerticalScrollMode = Microsoft.UI.Xaml.Controls.ScrollingScrollMode.Disabled;
                control.VerticalScrollBarVisibility = Microsoft.UI.Xaml.Controls.ScrollingScrollBarVisibility.Hidden;
            });
        var dataFileTree = inventory is null ? Text(busy || Props.Loading ? "Loading application files…" : "Application files are unavailable.")
            : VStack(8, new[] { "Settings", "Registry", "LocalState" }.Select(area => Disclosure(area,
                VStack(4, inventory.Files.Any(item => item.Area == area)
                    ? inventory.Files.Where(item => item.Area == area).Select(item => FlexRow(
                        FilePreviewButton(item).Flex(grow: 1, basis: 0),
                        OpenFileButton(item).Margin(left: 4).Flex(shrink: 0)
                    ).HAlign(HorizontalAlignment.Stretch).WithKey(item.FullPath)).ToArray()
                    : new[] { Text("No files listed.") }), isExpanded: expandedAreas.Value.Contains(area), changed: expanded =>
                    {
                        var next = new HashSet<string>(expandedAreas.Value, StringComparer.Ordinal);
                        if (expanded) next.Add(area); else next.Remove(area);
                        expandedAreas.Set(next.ToArray());
                    }).WithKey(area)).ToArray());
        var fileTree = VStack(8,
            Slot("manifest-link", selected?.Source == "MSIX" ? ActionButton("Package · AppxManifest.xml", () => Props.OnView("Package")).IsEnabled(!busy) : Text("")),
            Slot("file-inventory", dataFileTree));
        // Keep the selected application's files inside its native ListView row.
        var selectedIndex = Array.FindIndex(visibleApps, app => app.FullName == selected?.FullName);
        var appList = (ListView<InstalledApp>(visibleApps, app => app.FullName, (app, _) => Border(VStack(8,
            FlexRow(
                CheckBox(app.FullName == selected?.FullName, isChecked =>
                {
                    if (busy) return;
                    if (isChecked && app.FullName != selected?.FullName)
                        Select(new AppTreeNode(app.FullName, app.Name, app, Array.Empty<AppTreeNode>()));
                    else if (!isChecked && app.FullName == selected?.FullName) Unselect();
                }).AutomationName($"Unselect {app.Name}").IsEnabled(!busy)
                    .IsVisible(app.FullName == selected?.FullName)
                    .MinWidth(0).Width(24).Padding(0).Margin(right: 8).Flex(shrink: 0),
                AppIcon(app).Margin(right: 12).Flex(shrink: 0),
                VStack(3, TextBlock(app.Name).SemiBold().TextWrapping(),
                    TextBlock(app.Source == "MSIX" ? "Packaged app" : "Desktop app").FontSize(12).Foreground(Theme.SecondaryText)).Flex(grow: 1, basis: 0)),
            Slot("app-files", !Props.Compact && app.FullName == selected?.FullName
                ? Props.Loading ? HStack(8,
                    ProgressRing().Width(20).Height(20).Set(control => { control.IsActive = true; control.IsIndeterminate = true; })
                        .AutomationName($"Loading files for {app.Name}"),
                    Text("Loading app files…"))
                : Disclosure("App files", fileTree, appFilesOpen.Value, expanded =>
                    { appFilesOpen.Set(expanded); if (expanded) appsListOpen.Set(false); })
                    .AutomationName($"Files for {app.Name}")
                : Text(""))
        ).HAlign(HorizontalAlignment.Stretch).Padding(10))
            .WithBorder(app.FullName == selected?.FullName ? Theme.Ref("AccentFillColorDefaultBrush") : Theme.Ref("ControlFillColorTransparentBrush"))
            .CornerRadius(10).HAlign(HorizontalAlignment.Stretch).WithKey(app.FullName)) with { SelectedIndex = selectedIndex })
            .SelectedIndexChanged(index =>
            {
                if (index >= 0 && index < visibleApps.Length && !busy && visibleApps[index].FullName != selected?.FullName)
                    Select(new AppTreeNode(visibleApps[index].FullName, visibleApps[index].Name, visibleApps[index], Array.Empty<AppTreeNode>()));
            })
            .AutomationName("Alphabetical application list").IsEnabled(!busy).HAlign(HorizontalAlignment.Stretch)
            .Set(control =>
            {
                control.HorizontalContentAlignment = HorizontalAlignment.Stretch;
                control.ItemContainerStyle ??= new Style(typeof(Microsoft.UI.Xaml.Controls.ListViewItem))
                {
                    Setters =
                    {
                        new Setter(Microsoft.UI.Xaml.Controls.Control.HorizontalContentAlignmentProperty, HorizontalAlignment.Stretch),
                        new Setter(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Stretch)
                    }
                };
                Microsoft.UI.Xaml.Controls.ScrollViewer.SetHorizontalScrollMode(control, Microsoft.UI.Xaml.Controls.ScrollMode.Disabled);
                Microsoft.UI.Xaml.Controls.ScrollViewer.SetHorizontalScrollBarVisibility(control, Microsoft.UI.Xaml.Controls.ScrollBarVisibility.Disabled);
                Microsoft.UI.Xaml.Controls.ScrollViewer.SetVerticalScrollMode(control, Props.Compact
                    ? Microsoft.UI.Xaml.Controls.ScrollMode.Enabled : Microsoft.UI.Xaml.Controls.ScrollMode.Disabled);
                Microsoft.UI.Xaml.Controls.ScrollViewer.SetVerticalScrollBarVisibility(control, Props.Compact
                    ? Microsoft.UI.Xaml.Controls.ScrollBarVisibility.Auto : Microsoft.UI.Xaml.Controls.ScrollBarVisibility.Disabled);
            });

        var navigation = Border(
            (FlexColumn(
                HStack(10, Icon(FontIcon("\uE71D", "Segoe Fluent Icons")).Width(24).Height(24), TextBlock("Applications").FontSize(24).SemiBold()),
                TextBlock($"{apps.Length} apps in your library").Foreground(Theme.SecondaryText),
                TextBox(search.Value, Filter, placeholderText: "Search your library").AutomationName("Search applications"),
                ActionButton(busy ? "Working…" : "Refresh applications", Scan).AutomationName("Refresh applications").IsEnabled(!busy),
                Slot("unselect", selected is not null ? ActionButton("Clear selection", Unselect).IsEnabled(!busy) : Text("")),
                ScrollView(VStack(14,
                Disclosure("Open Apps List", keyboard, !filtering && appsListOpen.Value, expanded =>
                    { appsListOpen.Set(expanded); if (expanded) appFilesOpen.Set(false); })
                    .AutomationName("Open Apps List").IsEnabled(!filtering),
                TextBlock($"{(filtering ? "Matches" : letter.Value)} · {visibleApps.Length} applications").Foreground(Theme.AccentText).SemiBold(),
                appList))
                    .AutomationName("Application rail scroll area")
                    .Set(control =>
                    {
                        _railScroll = control;
                        if (_resetScroll)
                        {
                            _resetScroll = false;
                            control.DispatcherQueue.TryEnqueue(() => _railScroll?.ScrollTo(0, 0,
                                new Microsoft.UI.Xaml.Controls.ScrollingScrollOptions(
                                    Microsoft.UI.Xaml.Controls.ScrollingAnimationMode.Disabled,
                                    Microsoft.UI.Xaml.Controls.ScrollingSnapPointsMode.Ignore)));
                        }
                        control.ContentOrientation = Microsoft.UI.Xaml.Controls.ScrollingContentOrientation.Vertical;
                        control.VerticalScrollMode = Microsoft.UI.Xaml.Controls.ScrollingScrollMode.Enabled;
                        control.VerticalScrollBarVisibility = Microsoft.UI.Xaml.Controls.ScrollingScrollBarVisibility.Auto;
                        control.HorizontalScrollMode = Microsoft.UI.Xaml.Controls.ScrollingScrollMode.Disabled;
                        control.HorizontalScrollBarVisibility = Microsoft.UI.Xaml.Controls.ScrollingScrollBarVisibility.Hidden;
                    }).MinHeight(0).Flex(grow: 1, basis: 0)
            ) with { RowGap = 14 }).FlexPadding(24)
        ).Background(Theme.LayerFill).WithBorder(Theme.CardStroke).CornerRadius(12)
            .Width(400).Margin(left: 16, top: 12, right: 0, bottom: 16).Flex(shrink: 0);

        if (Props.Compact)
            return Border(VStack(8,
                TextBlock("Applications").SemiBold(),
                TextBox(search.Value, Filter, placeholderText: "Search your library").AutomationName("Search applications"),
                Disclosure(selected?.Name ?? "Choose an application", appList.Height(160), filtering || appsListOpen.Value,
                    expanded => appsListOpen.Set(expanded)).AutomationName("Choose application")))
                .Padding(12).Background(Theme.LayerFill).WithBorder(Theme.CardStroke).CornerRadius(12)
                .Margin(12).Flex(shrink: 0);
        return navigation;
    }
}
