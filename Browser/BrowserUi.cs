// SPDX-License-Identifier: GPL-3.0-only
using Microsoft.UI.Xaml;
using Microsoft.UI.Reactor;
using Microsoft.UI.Reactor.Core;
using MyApps.Models;
using static Microsoft.UI.Reactor.Factories;


namespace MyApps.Browser;

internal static class BrowserUi
{
    // A stable panel owns conditional content; changing native kinds replaces its keyed child.
    public static Element Slot(string name, Element child) => VStack(0,
        child.WithKey($"{name}-{child.GetType().FullName}-{child.Key}")).WithKey(name);
    public static Element Text(string value) => TextBlock(value).TextWrapping();
    public static ExpanderElement Disclosure(string title, Element body, Optional<bool> isExpanded = default, Action<bool>? changed = null) =>
            Expander(title, body.HAlign(HorizontalAlignment.Stretch), isExpanded, changed)
                .HAlign(HorizontalAlignment.Stretch)
                .Set(control => control.HorizontalContentAlignment = HorizontalAlignment.Stretch);
    public static Element ReadOnlyText(string value, string name) => TextBox(value)
            .HAlign(HorizontalAlignment.Stretch).TextWrapping().AutomationName(name)
            .Set(control => { control.IsReadOnly = true; control.HorizontalContentAlignment = HorizontalAlignment.Stretch; })
            .Padding(10).CornerRadius(8);
    public static Element Section(string title, params Element[] children) => Card(VStack(10,
            new[] { TextBlock(title).FontSize(20).Bold() }.Concat(children).ToArray()).Padding(24)).Background(Theme.CardBackground).WithBorder(Theme.CardStroke).CornerRadius(12);
    public static string ActionGlyph(string label) => label.StartsWith("Refresh") || label.StartsWith("Working") ? "\uE72C"
            : label.StartsWith("Restart") || label.StartsWith("Try again") ? "\uE72C"
            : label.StartsWith("Unselect") || label == "Clear selection" ? "\uE711"
            : label.StartsWith("Save") ? "\uE74E"
            : label.StartsWith("Reset") || label.StartsWith("Reload") ? "\uE777"
            : label.Contains("default app") ? "\uE8A7"
            : label.StartsWith("Open") ? "\uE8B7"
            : label.StartsWith("Measure") ? "\uE9D9" : "\uE8A5";
    public static Element FluentGlyph(string glyph, double size = 16) => TextBlock(glyph)
            .Set(control => { control.FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Segoe Fluent Icons"); control.FontSize = size; })
            .VAlign(VerticalAlignment.Center).HAlign(HorizontalAlignment.Center);
    public static ButtonElement ActionButton(string label, Action action) => Button(HStack(8,
            FluentGlyph(ActionGlyph(label), 18).Width(24).Height(24), TextBlock(label).TextWrapping()), action)
            .AutomationName(label)
            .Background(Theme.LayerFill).WithBorder(Theme.CardStroke).CornerRadius(9).Padding(10);
    public static Element OpenFileButton(AppDataFile item, Action<AppDataFile> open, bool busy) =>
            Button(Icon(FontIcon("\uE8A7", "Segoe Fluent Icons")), () => open(item)).AutomationName($"Open {item.RelativePath} with default app")
                .Width(32).Height(32).Padding(0).CornerRadius(9)
                .Background(Theme.LayerFill).WithBorder(Theme.CardStroke)
                .IsEnabled(!busy)
                .Set(control => Microsoft.UI.Xaml.Controls.ToolTipService.SetToolTip(control, FileTooltip(item, "Open with default app")));
    public static Microsoft.UI.Xaml.Controls.TextBlock FileTooltip(AppDataFile item, string? action = null) => new()
        {
            Text = $"{Path.GetFileName(item.FullPath)}\n{item.Kind}\nSize: {item.Bytes:N0} bytes\nModified: {item.LastWriteUtc.ToLocalTime():g}\nLocation: {item.FullPath}" +
                (action is null ? "" : $"\n{action}"),
            TextWrapping = TextWrapping.Wrap,
            MaxWidth = 420
        };
    public static ButtonElement FilePreviewButton(AppDataFile item, Action<AppDataFile> preview, bool busy, bool showDetails = false) => Button(
            (FlexRow(
                FluentGlyph("\uE8A5", 16).Width(20).Height(24).Flex(shrink: 0),
                TextBlock(showDetails ? $"{item.RelativePath} · {item.Bytes:N0} bytes · {item.Kind}" : Path.GetFileName(item.FullPath))
                    .Set(control => { control.TextTrimming = TextTrimming.CharacterEllipsis; control.MaxLines = 1; })
                    .Margin(left: 6).Flex(grow: 1, basis: 0)
            )).HAlign(HorizontalAlignment.Stretch), () => preview(item))
            .AutomationName($"Inspect {item.RelativePath}").IsEnabled(!busy)
            .HAlign(HorizontalAlignment.Stretch).Padding(8).CornerRadius(9)
            .Background(Theme.LayerFill).WithBorder(Theme.CardStroke)
            .Set(control =>
            {
                control.HorizontalContentAlignment = HorizontalAlignment.Stretch;
                Microsoft.UI.Xaml.Controls.ToolTipService.SetToolTip(control, FileTooltip(item));
            });

    public static Element AppIcon(InstalledApp app, double size = 32) => app.IconUri is null
            ? Icon(FontIcon("\uE71D", "Segoe Fluent Icons")).Width(size).Height(size)
            : Image(string.IsNullOrWhiteSpace(app.FamilyName) ? app.IconUri
                : MyApps.Services.Icons.ApplicationIcons.NormalizeCachedPackageIcon(app.IconUri))
                .AutomationName($"{app.Name} icon").Width(size).Height(size);
}
