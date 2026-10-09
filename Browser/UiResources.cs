// SPDX-License-Identifier: GPL-3.0-only
using System.Text.Json;

namespace WinApps.Browser;

internal sealed record GuideResource(string Explanation, string Topic, string Url);
internal sealed record LinkResource(string Label, string Url);
internal sealed record ControlResource(string Label, string Icon);
internal sealed record UiResourceFile(Dictionary<string, GuideResource> Sections,
    Dictionary<string, LinkResource> Links, Dictionary<string, ControlResource> Controls);

internal static class UiResources
{
    public static string FilePath => Path.Combine(AppContext.BaseDirectory, "Resources", "ui-resources.json");
    public static UiResourceFile Current { get; private set; } = LoadInitial();

    public static void Reload() => Current = Parse(File.ReadAllText(FilePath));

    private static UiResourceFile LoadInitial()
    {
        try { return Parse(File.ReadAllText(FilePath)); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or InvalidDataException)
        {
            using var stream = typeof(UiResources).Assembly.GetManifestResourceStream("WinApps.UiResources")
                ?? throw new InvalidOperationException("Bundled UI resources are missing.");
            using var reader = new StreamReader(stream);
            return Parse(reader.ReadToEnd());
        }
    }

    internal static UiResourceFile Parse(string json)
    {
        var resources = JsonSerializer.Deserialize<UiResourceFile>(json)
            ?? throw new InvalidDataException("UI resources are empty.");
        if (resources.Sections is null || resources.Links is null || resources.Controls is null ||
            !resources.Sections.ContainsKey("Default") ||
            new[] { "Msix", "WinUI3", "Frameworks", "Runtime" }.Any(key => !resources.Links.ContainsKey(key)) ||
            new[] { "Grid", "Rows", "Widgets" }.Any(key => !resources.Controls.ContainsKey(key)) ||
            resources.Sections.Values.Any(item => item is null || item.Explanation is null || item.Topic is null || !IsLink(item.Url)) ||
            resources.Links.Values.Any(item => item is null || item.Label is null || !IsLink(item.Url)) ||
            resources.Controls.Values.Any(item => item is null || item.Label is null || item.Icon is null))
            throw new InvalidDataException("UI resources contain missing entries or invalid HTTPS links.");
        return resources;
    }

    private static bool IsLink(string? url) => Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme == "https";
    public static string LayoutLabel(string key) => Current.Controls[key].Icon + " " + Current.Controls[key].Label;
}
