// SPDX-License-Identifier: GPL-3.0-only
using Microsoft.Win32;

namespace WinApps.Services.Inventory;

internal sealed record StartupApplication(string Name, string Source, string State, string Command);
internal sealed record StartupSnapshot(StartupApplication[] Apps, string[] Notices);

internal static class StartupApplications
{
    internal static StartupSnapshot Read()
    {
        var entries = new List<StartupApplication>();
        var notices = new List<string>();
        foreach (var hive in new[] { RegistryHive.CurrentUser, RegistryHive.LocalMachine })
        foreach (var view in Environment.Is64BitOperatingSystem ? new[] { RegistryView.Registry64, RegistryView.Registry32 } : new[] { RegistryView.Registry32 })
        {
            try
            {
                using var root = RegistryKey.OpenBaseKey(hive, view);
                using var run = root.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run");
                using var approved = root.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\" +
                    (view == RegistryView.Registry32 && Environment.Is64BitOperatingSystem ? "Run32" : "Run"));
                if (run is null) continue;
                foreach (var name in run.GetValueNames())
                    if (run.GetValue(name) is string command)
                        entries.Add(new(name, $"{(hive == RegistryHive.CurrentUser ? "Your account" : "All users")} · {view}",
                            ApprovalState(approved?.GetValue(name) as byte[]), command));
            }
            catch (Exception error) { notices.Add($"Some {hive} startup entries could not be read: {error.Message}"); }
        }
        foreach (var folder in new[] { Environment.SpecialFolder.Startup, Environment.SpecialFolder.CommonStartup })
        {
            try
            {
                var path = Environment.GetFolderPath(folder);
                if (!Directory.Exists(path)) continue;
                using var root = RegistryKey.OpenBaseKey(folder == Environment.SpecialFolder.Startup ? RegistryHive.CurrentUser : RegistryHive.LocalMachine, RegistryView.Default);
                using var approved = root.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\StartupFolder");
                foreach (var file in Directory.EnumerateFiles(path).Where(file => !string.Equals(Path.GetFileName(file), "desktop.ini", StringComparison.OrdinalIgnoreCase)))
                    entries.Add(new(Path.GetFileNameWithoutExtension(file), folder == Environment.SpecialFolder.Startup ? "Your startup folder" : "Shared startup folder",
                        ApprovalState(approved?.GetValue(Path.GetFileName(file)) as byte[]), file));
            }
            catch (Exception error) { notices.Add($"A startup folder could not be read: {error.Message}"); }
        }
        return new(entries.Distinct().OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase).ToArray(), notices.ToArray());
    }

    internal static string ApprovalState(byte[]? value) => value is not { Length: >= 4 } ? "Status unavailable"
        : value[0] is 2 or 6 ? "Enabled" : value[0] is 3 or 7 ? "Disabled" : "Status unavailable";
}
