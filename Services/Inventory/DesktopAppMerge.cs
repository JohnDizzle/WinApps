// SPDX-License-Identifier: GPL-3.0-only
using WinApps.Models;

namespace WinApps.Services.Inventory;

public static class DesktopAppMerge
{
    public static IReadOnlyList<InstalledApp> Merge(IReadOnlyList<InstalledApp> registered,
        IReadOnlyList<InstalledApp> shortcuts)
    {
        var merged = registered.ToList();
        foreach (var shortcut in shortcuts)
        {
            if (shortcut.ConsolePath is not null)
            {
                merged.Add(shortcut);
                continue;
            }
            // A shared folder identifies neither an application nor its executable.
            // Keep suite members (Excel, Word, etc.) separate from the suite entry.
            var matches = merged.Select((app, index) => (app, index)).Where(item =>
                item.app.Name.Equals(shortcut.Name, StringComparison.OrdinalIgnoreCase) &&
                (string.IsNullOrEmpty(item.app.InstallPath) ||
                 RelatedInstallFolders(item.app.InstallPath, shortcut.InstallPath))).ToArray();
            if (matches.Length != 1)
            {
                merged.Add(shortcut);
                continue;
            }
            var (existing, position) = matches[0];
            merged[position] = existing with
            {
                IconUri = shortcut.IconUri ?? existing.IconUri,
                InstallPath = string.IsNullOrEmpty(existing.InstallPath) ? shortcut.InstallPath : existing.InstallPath,
                LaunchPath = shortcut.LaunchPath,
                ExecutablePath = shortcut.ExecutablePath
            };
        }
        return merged;
    }

    private static bool RelatedInstallFolders(string registered, string shortcut)
    {
        if (string.IsNullOrWhiteSpace(shortcut)) return false;
        var left = registered.TrimEnd('\\', '/');
        var right = shortcut.TrimEnd('\\', '/');
        return left.Equals(right, StringComparison.OrdinalIgnoreCase)
            || left.StartsWith(right + "\\", StringComparison.OrdinalIgnoreCase)
            || right.StartsWith(left + "\\", StringComparison.OrdinalIgnoreCase);
    }

}
