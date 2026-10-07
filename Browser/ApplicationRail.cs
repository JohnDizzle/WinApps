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
    Action<AppDataFile> OnPreviewFile, Action<AppDataFile> OnOpenFile, Action<string> OnView);

internal sealed class ApplicationRail : Component<ApplicationRailProps>
{
    public override Element Render()
    {
        var letter = UseState("All");
        var search = UseState("");
        var appsListOpen = UseState(true);
        var appFilesOpen = UseState(false);
        var apps = Props.Apps;
        var selected = Props.Selected;
        var inventory = Props.Inventory;
        var view = Props.View;
        var busy = Props.Busy;
        UseEffect(() =>
        {
            appsListOpen.Set(Props.Selected is null);
            appFilesOpen.Set(Props.Selected is not null);
        }, Props.Selected?.FullName);
        void Select(AppTreeNode node) { if (node.App is not null) Props.OnSelect(node.App); }
        void Unselect() => Props.OnUnselect();
        void Scan() { letter.Set("All"); search.Set(""); appsListOpen.Set(true); appFilesOpen.Set(false); Props.OnRefresh(); }
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
        var visibleApps = matches.Where(app => letter.Value == "All" || Initial(app) == letter.Value)
            .OrderBy(app => app.Name, StringComparer.OrdinalIgnoreCase).ToArray();
        var letters = new[] { "All", "#" }.Concat(Enumerable.Range('A', 26).Select(code => ((char)code).ToString())).ToArray();
        var keyboard = VStack(6, letters.Chunk(4).Select((row, index) => HStack(6, row.Select(key =>
            ToggleButton(key, letter.Value == key, enabled => { if (enabled) letter.Set(key); })
                .Width(64).Height(40).Padding(0).AutomationName($"Show {key} applications")
                .CornerRadius(8)
                .IsEnabled(key == "All" || matches.Any(app => Initial(app) == key)).WithKey(key)).ToArray())
                .WithKey($"letters-{index}")).ToArray());
        var dataFileTree = inventory is null ? Text(busy ? "Loading application files…" : "Application files are unavailable.")
            : VStack(8, new[] { "Settings", "Registry", "LocalState" }.Select(area => Disclosure(area,
                VStack(4, inventory.Files.Any(item => item.Area == area)
                    ? inventory.Files.Where(item => item.Area == area).Select(item => FlexRow(
                        FilePreviewButton(item).Flex(grow: 1, basis: 0),
                        OpenFileButton(item).Margin(left: 4).Flex(shrink: 0)
                    ).HAlign(HorizontalAlignment.Stretch).WithKey(item.FullPath)).ToArray()
                    : new[] { Text("No files listed.") }), isExpanded: area == view).WithKey(area)).ToArray());
        var fileTree = VStack(8,
            Slot("manifest-link", selected?.Source == "MSIX" ? ActionButton("Package · AppxManifest.xml", () => Props.OnView("Package")).IsEnabled(!busy) : Text("")),
            Slot("file-inventory", dataFileTree));
        // Keep the selected application's files inside its native ListView row.
        var selectedIndex = Array.FindIndex(visibleApps, app => app.FullName == selected?.FullName);
        var appList = ListView((int?)selectedIndex, index =>
        {
            if (index >= 0 && index < visibleApps.Length && !busy && visibleApps[index].FullName != selected?.FullName)
                Select(new AppTreeNode(visibleApps[index].FullName, visibleApps[index].Name, visibleApps[index], Array.Empty<AppTreeNode>()));
        }, visibleApps.Select(app => VStack(8,
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
            Slot("app-files", app.FullName == selected?.FullName
                ? Disclosure("App files", fileTree, appFilesOpen.Value, expanded =>
                    { appFilesOpen.Set(expanded); if (expanded) appsListOpen.Set(false); })
                    .AutomationName($"Files for {app.Name}")
                : Text(""))
        ).HAlign(HorizontalAlignment.Stretch).Padding(10).WithKey(app.FullName)).ToArray())
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
            });

        var navigation = Border(
            (FlexColumn(
                HStack(10, Icon(FontIcon("\uE71D", "Segoe Fluent Icons")).Width(24).Height(24), TextBlock("Applications").FontSize(24).SemiBold()),
                TextBlock($"{apps.Length} apps in your library").Foreground(Theme.SecondaryText),
                TextBox(search.Value, search.Set, placeholderText: "Search your library").AutomationName("Search applications"),
                ActionButton(busy ? "Working…" : "Refresh applications", Scan).AutomationName("Refresh applications").IsEnabled(!busy),
                Slot("unselect", selected is not null ? ActionButton("Unselect application", Unselect).IsEnabled(!busy) : Text("")),
                Disclosure("Open Apps List", keyboard, appsListOpen.Value, expanded =>
                    { appsListOpen.Set(expanded); if (expanded) appFilesOpen.Set(false); })
                    .AutomationName("Open Apps List"),
                TextBlock($"{letter.Value} · {visibleApps.Length} applications").Foreground(Theme.AccentText).SemiBold(),
                appList.Flex(grow: 1, basis: 0)
            ) with { RowGap = 14 }).FlexPadding(24)
        ).Background(Theme.LayerFill).WithBorder(Theme.CardStroke).CornerRadius(12)
            .Width(360).Margin(left: 16, top: 12, right: 0, bottom: 16).Flex(shrink: 0);

        return navigation;
    }
}
