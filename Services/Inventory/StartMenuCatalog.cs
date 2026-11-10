// SPDX-License-Identifier: GPL-3.0-only
using System.Runtime.InteropServices;
using WinApps.Models;
using WinApps.Services.Icons;
using WinApps.Services.Packages;

namespace WinApps.Services.Inventory;

public sealed class StartMenuCatalog
{
    public PackageScan Scan()
    {
        var apps = new List<InstalledApp>();
        var skipped = 0;
        try
        {
            foreach (var root in new[] { Environment.GetFolderPath(Environment.SpecialFolder.Programs),
                Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms) }.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                if (!Directory.Exists(root)) continue;
                var pending = new Stack<string>();
                pending.Push(root);
                while (pending.TryPop(out var folder))
                {
                    try
                    {
                        if ((File.GetAttributes(folder) & FileAttributes.ReparsePoint) != 0) continue;
                        foreach (var path in Directory.EnumerateFileSystemEntries(folder))
                        {
                            try
                            {
                                var attributes = File.GetAttributes(path);
                                if ((attributes & FileAttributes.ReparsePoint) != 0) continue;
                                if ((attributes & FileAttributes.Directory) != 0) { pending.Push(path); continue; }
                                if (!Path.GetExtension(path).Equals(".lnk", StringComparison.OrdinalIgnoreCase)) continue;
                                var name = Path.GetFileNameWithoutExtension(path);
                                if (ApplicationFilter.IsSupportingComponent(name) ||
                                    System.Text.RegularExpressions.Regex.IsMatch(name, @"(?i)\b(uninstall|setup|update|help)\b")) continue;
                                using var shortcut = new ShellShortcut(path);
                                var target = shortcut.TargetPath.Trim().Trim('"');
                                target = Environment.ExpandEnvironmentVariables(target);
                                var arguments = shortcut.Arguments;
                                var console = AdministrativeTools.ConsoleTarget(target, arguments);
                                if (!Path.IsPathFullyQualified(target) || !File.Exists(target) ||
                                    (console is null && !Path.GetExtension(target).Equals(".exe", StringComparison.OrdinalIgnoreCase))) continue;
                                if (console is not null && !File.Exists(console)) continue;
                                var iconLocation = shortcut.IconLocation;
                                var icon = ApplicationIcons.DesktopIcon(iconLocation) ?? ApplicationIcons.DesktopIcon(target);
                                apps.Add(new(name, "", $"StartMenu:{path}", "Unknown", "Unknown", "",
                                    Path.GetDirectoryName(console ?? target) ?? "", console is null ? "Desktop · Start Menu" : "Windows administrative tool",
                                    IconUri: icon, ConsolePath: console, LaunchPath: path, ExecutablePath: target, LaunchArguments: arguments,
                                    WorkingDirectory: Environment.ExpandEnvironmentVariables(shortcut.WorkingDirectory)));
                            }
                            catch (Exception ex) when (ex is COMException or IOException or UnauthorizedAccessException)
                            { skipped++; }
                        }
                    }
                    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { skipped++; }
                }
            }
        }
        catch (Exception ex) when (ex is COMException or UnauthorizedAccessException) { skipped++; }
        return new(apps.DistinctBy(app => $"{app.Name}|{app.InstallPath}", StringComparer.OrdinalIgnoreCase).ToArray(), skipped);
    }

}
