// SPDX-License-Identifier: GPL-3.0-only
using System.Xml;
using System.Xml.Linq;
using Microsoft.UI.Reactor;
using Microsoft.UI.Reactor.Core;
using WinApps.Models;
using static Microsoft.UI.Reactor.Factories;
using static WinApps.Browser.BrowserUi;

namespace WinApps.Browser;

internal sealed record PackagePropertyGroupsProps(IReadOnlyList<PackageConfigurationSection> Sections,
    string? Layout = null);

internal sealed class PackagePropertyGroups : Component<PackagePropertyGroupsProps>
{
    private int _columnCount = 1;
    public override Element Render()
    {
        var layout = UseState("Rows");
        var activeLayout = Props.Layout ?? layout.Value;
        var width = UseState(0d);
        var columns = width.Value >= 1500 ? 3 : width.Value >= 1000 ? 2 : 1;
        Element Body(PackageConfigurationSection section)
        {
            var guide = Guide(section.Title);
            var properties = Properties(section.Details);
            return VStack(10,
                MarkdownContent.Render($"**What this means**\n\n{guide.Explanation}\n\n[Microsoft Learn · {guide.Topic}]({guide.Url})", section.Title + " guide"),
                VStack(2, properties.Select((property, index) =>
                    FlexRow(TextBlock(property.Key).TextWrapping().Foreground(Theme.SecondaryText)
                            .Flex(grow: 1, basis: 0),
                        TextBlock(property.Value).TextWrapping().Set(control => control.IsTextSelectionEnabled = true)
                            .Margin(left: 16).Flex(grow: 2, basis: 0))
                        .Padding(8).Background(Theme.LayerFill).WithKey($"property-{index}")).ToArray()),
                Disclosure("Original details · read only", MarkdownContent.Code(section.Details,
                    section.Details.TrimStart().StartsWith('<') ? "xml" : "text", section.Title + " original details")));
        }
        Element Group(PackageConfigurationSection section)
        {
            if (activeLayout == "Rows") return Disclosure(section.Title, Body(section)).WithKey(section.Title);
            if (activeLayout == "Grid") return Section(section.Title, Body(section)).WithKey(section.Title);
            var properties = Properties(section.Details);
            return Section(section.Title,
                MarkdownContent.Render($"{Guide(section.Title).Explanation}\n\n[Microsoft Learn · {Guide(section.Title).Topic}]({Guide(section.Title).Url})", section.Title + " widget guide"),
                TextBlock($"{properties.Count} properties").FontSize(24).SemiBold(),
                VStack(4, properties.Take(3).Select((property, index) => Text($"{property.Key}: {property.Value}")
                    .WithKey($"summary-{index}")).ToArray()),
                Disclosure("Explore properties and documentation", Body(section))).WithKey(section.Title);
        }
        var groups = Props.Sections.Select(Group).ToArray();
        Element content = activeLayout == "Rows" ? VStack(8, groups).Set(BrowserMotion.Enable) : VStack(12,
            groups.Chunk(columns).Select((chunk, index) => FlexRow(chunk.Select((group, column) =>
                group.Margin(right: 12).Flex(grow: 1, basis: 0).WithKey($"column-{column}")).ToArray()).WithKey($"group-row-{index}")).ToArray()).Set(BrowserMotion.Enable);
        return VStack(12,
            HStack(8, new[] { "Grid", "Rows", "Widgets" }.Select(option =>
                ToggleButton(UiResources.LayoutLabel(option), layout.Value == option, enabled => { if (enabled) layout.Set(option); })
                    .Set(BrowserMotion.Enable)
                    .AutomationName($"Package layout {option}").WithKey(option)).ToArray()).IsVisible(Props.Layout is null),
            content.WithKey($"layout-{activeLayout}-{columns}"))
            .Set(control =>
            {
                BrowserMotion.Enable(control);
                if (control.Tag as string == "property-layout") return;
                control.Tag = "property-layout";
                control.SizeChanged += (_, args) =>
                {
                    var newColumns = args.NewSize.Width >= 1500 ? 3 : args.NewSize.Width >= 1000 ? 2 : 1;
                    if (_columnCount == newColumns) return;
                    _columnCount = newColumns;
                    width.Set(args.NewSize.Width);
                };
            });
    }

    internal static IReadOnlyList<KeyValuePair<string, string>> Properties(string details)
    {
        var result = new List<KeyValuePair<string, string>>();
        if (details.TrimStart().StartsWith('<'))
        {
            try
            {
                using var reader = XmlReader.Create(new StringReader("<Details>" + details + "</Details>"),
                    new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null,
                        MaxCharactersInDocument = 3 * 1024 * 1024 });
                var root = XElement.Load(reader);
                void Visit(XElement element, string path)
                {
                    var name = element.GetPrefixOfNamespace(element.Name.Namespace) is { Length: > 0 } prefix
                        ? prefix + ":" + element.Name.LocalName : element.Name.LocalName;
                    var siblings = element.Parent!.Elements(element.Name).ToArray();
                    var key = path + name + (siblings.Length > 1 ? $"[{Array.IndexOf(siblings, element) + 1}]" : "");
                    foreach (var attribute in element.Attributes().Where(attribute => !attribute.IsNamespaceDeclaration))
                        result.Add(new(key + "/@" + attribute.Name, attribute.Value));
                    if (!element.HasElements)
                    {
                        if (element.Value.Length > 0 || !element.HasAttributes) result.Add(new(key, element.Value));
                    }
                    else foreach (var child in element.Elements()) Visit(child, key + "/");
                }
                foreach (var element in root.Elements()) Visit(element, "");
                return result;
            }
            catch (XmlException) { /* Preserve non-XML or unsupported details below. */ }
        }
        foreach (var line in details.Split('\n').Where(line => !string.IsNullOrWhiteSpace(line)))
        {
            var separator = line.IndexOf(": ", StringComparison.Ordinal);
            result.Add(separator > 0 ? new(line[..separator].Trim(), line[(separator + 2)..].Trim())
                : new("Detail", line.Trim()));
        }
        return result;
    }

    internal static (string Explanation, string Topic, string Url) Guide(string title)
    {
        var guide = UiResources.Current.Sections.GetValueOrDefault(title) ?? UiResources.Current.Sections["Default"];
        return (guide.Explanation, guide.Topic, guide.Url);
    }
}
