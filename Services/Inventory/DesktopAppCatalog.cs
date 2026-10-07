// SPDX-License-Identifier: GPL-3.0-only
using Microsoft.Win32;
using MyApps.Models;

namespace MyApps.Services.Inventory;

public sealed class DesktopAppCatalog
{
    private const string UninstallPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall";

    public PackageScan Scan()
    {
        var apps = new Dictionary<string, InstalledApp>(StringComparer.OrdinalIgnoreCase);
        var skipped = 0;
        var views = Environment.Is64BitOperatingSystem
            ? new[] { RegistryView.Registry64, RegistryView.Registry32 }
            : new[] { RegistryView.Registry32 };
        foreach (var hive in new[] { RegistryHive.CurrentUser, RegistryHive.LocalMachine })
        foreach (var view in views)
        {
            try
            {
                using var root = RegistryKey.OpenBaseKey(hive, view);
                using var uninstall = root.OpenSubKey(UninstallPath, writable: false);
                if (uninstall is null) continue;
                foreach (var keyName in uninstall.GetSubKeyNames())
                {
                    try
                    {
                        using var key = uninstall.OpenSubKey(keyName, writable: false);
                        if (key is null) continue;
                        var name = key.GetValue("DisplayName") as string;
                        if (string.IsNullOrWhiteSpace(name)) continue;
                        if (key.GetValue("SystemComponent") is int hidden && hidden == 1) continue;
                        if (key.GetValue("ParentKeyName") is string parent && !string.IsNullOrEmpty(parent)) continue;
                        if (key.GetValue("ReleaseType") is string release && release is "Update" or "Hotfix" or "Security Update") continue;
                        if (ApplicationFilter.IsSupportingComponent(name)) continue;
                        var version = key.GetValue("DisplayVersion") as string ?? "Unknown";
                        var publisher = key.GetValue("Publisher") as string ?? "Unknown";
                        var location = key.GetValue("InstallLocation") as string ?? "";
                        location = Environment.ExpandEnvironmentVariables(location.Trim().Trim('"'));
                        // A registry entry is metadata, not proof that the directory still exists.
                        if (!Path.IsPathFullyQualified(location)) location = "";
                        long? estimated = key.GetValue("EstimatedSize") is int size ? (long)(uint)size : null;
                        var identity = $"{hive}\\{UninstallPath}\\{keyName}";
                        var dedupe = $"{identity}|{name}|{version}|{location}";
                        apps.TryAdd(dedupe, new InstalledApp(name, "", $"{identity} ({view})", publisher,
                            version, "", location, $"Desktop · {hive} · {view}", estimated, MyApps.Services.Icons.ApplicationIcons.DesktopIcon(key.GetValue("DisplayIcon") as string)));
                    }
                    catch (Exception ex) when (ex is UnauthorizedAccessException or System.Security.SecurityException or IOException)
                    { skipped++; }
                }
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or System.Security.SecurityException or IOException)
            { skipped++; }
        }
        return new(apps.Values.OrderBy(app => app.Name, StringComparer.OrdinalIgnoreCase).ToArray(), skipped);
    }
}

public sealed class AppInventory
{
    public PackageScan Scan()
    {
        var registered = new DesktopAppCatalog().Scan();
        var shortcuts = new StartMenuCatalog().Scan();
        var merged = registered.Apps.ToList();
        foreach (var shortcut in shortcuts.Apps)
        {
            var index = merged.FindIndex(app => app.Name.Equals(shortcut.Name, StringComparison.OrdinalIgnoreCase));
            if (index < 0)
            {
                var candidates = merged.Select((app, position) => (app, position)).Where(item =>
                    !string.IsNullOrEmpty(item.app.InstallPath) && item.app.InstallPath.TrimEnd('\\').Equals(
                        shortcut.InstallPath.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase)).ToArray();
                if (candidates.Length == 1) index = candidates[0].position;
            }
            if (index >= 0)
            {
                var existing = merged[index];
                merged[index] = existing with
                {
                    IconUri = shortcut.IconUri ?? existing.IconUri,
                    InstallPath = string.IsNullOrEmpty(existing.InstallPath) ? shortcut.InstallPath : existing.InstallPath
                };
            }
            else merged.Add(shortcut);
        }
        var desktop = new PackageScan(merged, registered.Skipped + shortcuts.Skipped);
        try
        {
            var packaged = new MyApps.Services.Packages.PackageCatalog().Scan();
            return new(desktop.Apps.Concat(packaged.Apps).OrderBy(app => app.Name, StringComparer.OrdinalIgnoreCase).ToArray(),
                desktop.Skipped + packaged.Skipped);
        }
        catch (Exception ex) when (ex is System.Runtime.InteropServices.COMException or UnauthorizedAccessException)
        {
            return new(desktop.Apps, desktop.Skipped + 1);
        }
    }
}



