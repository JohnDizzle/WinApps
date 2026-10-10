// SPDX-License-Identifier: GPL-3.0-only
using Microsoft.Win32;
using WinApps.Models;

namespace WinApps.Services.Inventory;

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
                            version, "", location, $"Desktop · {hive} · {view}", estimated, WinApps.Services.Icons.ApplicationIcons.DesktopIcon(key.GetValue("DisplayIcon") as string)));
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
        var merged = DesktopAppMerge.Merge(registered.Apps, shortcuts.Apps);
        var desktop = new PackageScan(merged, registered.Skipped + shortcuts.Skipped);
        try
        {
            var packaged = new WinApps.Services.Packages.PackageCatalog().Scan();
            return new(desktop.Apps.Concat(packaged.Apps).OrderBy(app => app.Name, StringComparer.OrdinalIgnoreCase).ToArray(),
                desktop.Skipped + packaged.Skipped);
        }
        catch (Exception ex) when (ex is System.Runtime.InteropServices.COMException or UnauthorizedAccessException)
        {
            return new(desktop.Apps, desktop.Skipped + 1);
        }
    }
}



