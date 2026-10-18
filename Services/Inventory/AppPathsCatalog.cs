// SPDX-License-Identifier: GPL-3.0-only
using System.Diagnostics;
using Microsoft.Win32;
using WinApps.Models;

namespace WinApps.Services.Inventory;

public sealed class AppPathsCatalog
{
    public PackageScan Scan()
    {
        var apps = new Dictionary<string, InstalledApp>(StringComparer.OrdinalIgnoreCase);
        var skipped = 0;
        foreach (var hive in new[] { RegistryHive.CurrentUser, RegistryHive.LocalMachine })
        foreach (var view in Environment.Is64BitOperatingSystem
            ? new[] { RegistryView.Registry64, RegistryView.Registry32 } : new[] { RegistryView.Registry32 })
        {
            try
            {
                using var registry = RegistryKey.OpenBaseKey(hive, view);
                using var paths = registry.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths");
                if (paths is null) continue;
                foreach (var keyName in paths.GetSubKeyNames())
                {
                    try
                    {
                        using var key = paths.OpenSubKey(keyName);
                        var executable = Environment.ExpandEnvironmentVariables((key?.GetValue(null) as string ?? "").Trim().Trim('"'));
                        if (!Path.IsPathFullyQualified(executable) || !Path.GetExtension(executable).Equals(".exe", StringComparison.OrdinalIgnoreCase)
                            || !File.Exists(executable) || apps.ContainsKey(executable)) continue;
                        var version = FileVersionInfo.GetVersionInfo(executable);
                        var name = string.IsNullOrWhiteSpace(version.FileDescription) ? Path.GetFileNameWithoutExtension(executable) : version.FileDescription.Trim();
                        if (ApplicationFilter.IsSupportingComponent(name)) continue;
                        apps[executable] = new(name, "", $"AppPaths:{executable}", version.CompanyName ?? "Unknown",
                            version.FileVersion ?? "Unknown", "", Path.GetDirectoryName(executable) ?? "", "Desktop · App Paths",
                            IconUri: Icons.ApplicationIcons.DesktopIcon(executable), LaunchPath: executable, ExecutablePath: executable);
                    }
                    catch (Exception error) when (error is IOException or UnauthorizedAccessException or System.Security.SecurityException
                        or System.ComponentModel.Win32Exception or ArgumentException) { skipped++; }
                }
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or System.Security.SecurityException) { skipped++; }
        }
        return new(apps.Values.OrderBy(app => app.Name, StringComparer.OrdinalIgnoreCase).ToArray(), skipped);
    }
}
