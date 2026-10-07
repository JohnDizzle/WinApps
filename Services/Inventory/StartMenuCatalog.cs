// SPDX-License-Identifier: GPL-3.0-only
using System.Runtime.InteropServices;
using MyApps.Models;
using MyApps.Services.Icons;

namespace MyApps.Services.Inventory;

public sealed class StartMenuCatalog
{
    public PackageScan Scan()
    {
        var apps = new List<InstalledApp>();
        var skipped = 0;
        object? shell = null;
        try
        {
            var shellType = Type.GetTypeFromProgID("WScript.Shell");
            if (shellType is null) return new(apps, 1);
            shell = Activator.CreateInstance(shellType);
            if (shell is null) return new(apps, 1);
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
                            object? shortcut = null;
                            try
                            {
                                var attributes = File.GetAttributes(path);
                                if ((attributes & FileAttributes.ReparsePoint) != 0) continue;
                                if ((attributes & FileAttributes.Directory) != 0) { pending.Push(path); continue; }
                                if (!Path.GetExtension(path).Equals(".lnk", StringComparison.OrdinalIgnoreCase)) continue;
                                var name = Path.GetFileNameWithoutExtension(path);
                                if (ApplicationFilter.IsSupportingComponent(name) ||
                                    System.Text.RegularExpressions.Regex.IsMatch(name, @"(?i)\b(uninstall|setup|update|help)\b")) continue;
                                shortcut = ((dynamic)shell).CreateShortcut(path);
                                var target = ((string?)((dynamic)shortcut).TargetPath ?? "").Trim().Trim('"');
                                if (!Path.IsPathFullyQualified(target) || !File.Exists(target) ||
                                    !Path.GetExtension(target).Equals(".exe", StringComparison.OrdinalIgnoreCase)) continue;
                                var iconLocation = (string?)((dynamic)shortcut).IconLocation;
                                var icon = ApplicationIcons.DesktopIcon(iconLocation) ?? ApplicationIcons.DesktopIcon(target);
                                apps.Add(new(name, "", $"StartMenu:{path}", "Unknown", "Unknown", "",
                                    Path.GetDirectoryName(target) ?? "", "Desktop · Start Menu", IconUri: icon));
                            }
                            catch (Exception ex) when (ex is COMException or IOException or UnauthorizedAccessException or Microsoft.CSharp.RuntimeBinder.RuntimeBinderException)
                            { skipped++; }
                            finally { Release(shortcut); }
                        }
                    }
                    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { skipped++; }
                }
            }
        }
        catch (Exception ex) when (ex is COMException or UnauthorizedAccessException) { skipped++; }
        finally { Release(shell); }
        return new(apps.DistinctBy(app => $"{app.Name}|{app.InstallPath}", StringComparer.OrdinalIgnoreCase).ToArray(), skipped);
    }

    private static void Release(object? value)
    {
        if (value is not null && Marshal.IsComObject(value)) Marshal.FinalReleaseComObject(value);
    }
}
