// SPDX-License-Identifier: GPL-3.0-only
namespace WinApps.Services.Inventory;

public static class AdministrativeTools
{
    public static string? ConsoleTarget(string target, string arguments)
    {
        if (Path.GetExtension(target).Equals(".msc", StringComparison.OrdinalIgnoreCase)) return target;
        if (!Path.GetFileName(target).Equals("mmc.exe", StringComparison.OrdinalIgnoreCase)) return null;
        // MMC accepts a console document followed by switches such as /s.
        var match = System.Text.RegularExpressions.Regex.Match(arguments.Trim(), "^(?:\"([^\"]+\\.msc)\"|([^\\s]+\\.msc))(?:\\s|$)",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        if (!match.Success) return null;
        var console = Environment.ExpandEnvironmentVariables(match.Groups[1].Success ? match.Groups[1].Value : match.Groups[2].Value);
        if (!Path.IsPathFullyQualified(console)) console = Path.Combine(Path.GetDirectoryName(target) ?? "", console);
        return Path.IsPathFullyQualified(console) ? console : null;
    }
}
