// SPDX-License-Identifier: GPL-3.0-only
using System.Text.RegularExpressions;
using Microsoft.UI.Reactor;
using Microsoft.UI.Reactor.Core;
using Microsoft.UI.Xaml.Documents;
using static Microsoft.UI.Reactor.Factories;
using static MyApps.Browser.BrowserUi;

namespace MyApps.Browser;

// Renders the authored documentation subset: headings, paragraphs, bullets,
// bold text, HTTPS links, and fenced code. It never interprets HTML.
internal static class MarkdownContent
{
    public static Element Render(string markdown, string name)
    {
        var blocks = new List<Element>();
        var lines = markdown.Replace("\r\n", "\n").Split('\n');
        for (var index = 0; index < lines.Length; index++)
        {
            var line = lines[index];
            if (string.IsNullOrWhiteSpace(line)) continue;
            if (line.StartsWith("```", StringComparison.Ordinal))
            {
                var language = line[3..].Trim();
                var code = new List<string>();
                while (++index < lines.Length && !lines[index].StartsWith("```", StringComparison.Ordinal)) code.Add(lines[index]);
                blocks.Add(VStack(6,
                    TextBlock(language.Length == 0 ? "Code" : language.ToUpperInvariant()).FontSize(12).Foreground(Theme.SecondaryText),
                    CodeBox(string.Join('\n', code), name + " code"))
                    .Padding(12).Background(Theme.LayerFill).WithBorder(Theme.CardStroke).CornerRadius(8));
                continue;
            }
            var heading = line.TakeWhile(character => character == '#').Count();
            var text = heading > 0 && line.Length > heading && line[heading] == ' ' ? line[(heading + 1)..]
                : line.StartsWith("- ", StringComparison.Ordinal) ? "• " + line[2..] : line;
            blocks.Add(TextBlock("").TextWrapping().FontSize(heading > 0 ? Math.Max(16, 26 - heading * 2) : 14)
                .Set(control =>
                {
                    control.IsTextSelectionEnabled = true;
                    control.Inlines.Clear();
                    var cursor = 0;
                    foreach (Match match in Regex.Matches(text, @"\*\*(.+?)\*\*|\[([^\]]+)\]\((https://[^\s)]+)\)"))
                    {
                        if (match.Index > cursor) control.Inlines.Add(new Run { Text = text[cursor..match.Index] });
                        if (match.Groups[1].Success)
                        {
                            var bold = new Bold(); bold.Inlines.Add(new Run { Text = match.Groups[1].Value });
                            control.Inlines.Add(bold);
                        }
                        else
                        {
                            var link = new Hyperlink { NavigateUri = new Uri(match.Groups[3].Value) };
                            link.Inlines.Add(new Run { Text = match.Groups[2].Value });
                            control.Inlines.Add(link);
                        }
                        cursor = match.Index + match.Length;
                    }
                    if (cursor < text.Length) control.Inlines.Add(new Run { Text = text[cursor..] });
                    if (heading > 0) control.FontWeight = Microsoft.UI.Text.FontWeights.SemiBold;
                }));
        }
        return VStack(8, blocks.Select((block, index) => block.WithKey($"markdown-{index}")).ToArray())
            .AutomationName(name);
    }

    public static Element Code(string code, string language, string name)
    {
        // Code is rendered directly; the source never passes through inline parsing.
        return VStack(6, TextBlock(language.ToUpperInvariant()).FontSize(12).Foreground(Theme.SecondaryText),
            CodeBox(code, name))
            .Padding(12).Background(Theme.LayerFill).WithBorder(Theme.CardStroke).CornerRadius(8);
    }

    private static TextBoxElement CodeBox(string code, string name) => TextBox(code).TextWrapping().AutomationName(name)
        .Set(control =>
        {
            control.IsReadOnly = true;
            control.FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Consolas");
            control.HorizontalContentAlignment = Microsoft.UI.Xaml.HorizontalAlignment.Stretch;
        }).Padding(10).CornerRadius(8).HAlign(Microsoft.UI.Xaml.HorizontalAlignment.Stretch);
}
