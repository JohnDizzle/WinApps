// SPDX-License-Identifier: GPL-3.0-only
using System.Diagnostics;
using Windows.Management.Deployment;
using WinApps.Models;

namespace WinApps.Services.Packages;

public sealed class ApplicationLauncher
{
    public static bool CanLaunch(InstalledApp app) =>
        !string.IsNullOrWhiteSpace(app.FamilyName) || ResolveShortcut(app) is not null;

    public static bool CanLaunchElevated(InstalledApp app) =>
        app.ConsolePath is not null || ResolveShortcut(app) is not null || !string.IsNullOrWhiteSpace(app.FamilyName);

    public async Task<string> LaunchAsync(InstalledApp app, bool elevated)
    {
        try
        {
            var shortcut = ResolveShortcut(app);
            if (shortcut is not null)
            {
                ProcessStartInfo start;
                if (elevated && app.ConsolePath is not null)
                {
                    var mmc = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "mmc.exe");
                    start = new ProcessStartInfo(mmc)
                    {
                        UseShellExecute = true,
                        Verb = "runas",
                        Arguments = $"\"{app.ConsolePath}\""
                    };
                }
                else
                {
                    start = new ProcessStartInfo(shortcut) { UseShellExecute = true };
                    if (elevated) start.Verb = "runas";
                }
                Process.Start(start);
                return elevated ? "Elevated launch requested." : "Application launched.";
            }
            if (string.IsNullOrWhiteSpace(app.FamilyName)) return "No package launch identity is available for this application.";
            // Store updates replace versioned install folders, but retain the family and AUMID.
            Windows.ApplicationModel.Package? package = null;
            Windows.ApplicationModel.Core.AppListEntry? entry = null;
            foreach (var candidate in new PackageManager().FindPackagesForUser(string.Empty)
                .Where(item => item.Id.FamilyName.Equals(app.FamilyName, StringComparison.OrdinalIgnoreCase)))
            {
                var entries = await candidate.GetAppListEntriesAsync();
                entry = entries.FirstOrDefault(item => item.AppUserModelId.Equals(app.FullName, StringComparison.OrdinalIgnoreCase));
                if (entry is not null) { package = candidate; break; }
            }
            if (entry is null || package is null) return "Launch failed: this application is no longer registered. Refresh the application library.";
            if (!elevated)
            {
                return await entry.LaunchAsync() ? "Application launched." : "Windows could not launch this application.";
            }
            var id = app.FullName.Contains('!') ? app.FullName[(app.FullName.LastIndexOf('!') + 1)..] : "";
            var configuration = PackageManifestFile.Read(Path.Combine(package.InstalledLocation.Path, "AppxManifest.xml"), id);
            if (configuration.Launch is not { AllowsElevation: true } launch)
                return "This application's manifest does not declare supported elevation.";
            var executable = PackageLaunchPath.Resolve(package.InstalledLocation.Path, launch.Executable);
            if (!File.Exists(executable)) return "The declared executable is unavailable.";
            // Let the shell activate the registered app with its package identity and UAC.
            // Direct execution of the protected WindowsApps executable can be denied.
            Process.Start(new ProcessStartInfo("shell:AppsFolder\\" + entry.AppUserModelId)
                { UseShellExecute = true, Verb = "runas" });
            return "Elevated launch requested.";
        }
        catch (System.ComponentModel.Win32Exception ex) when (ex.NativeErrorCode == 1223) { return "Elevated launch cancelled."; }
        catch (Exception ex) { return $"Launch failed: {ex.Message}"; }
    }

    private static string? ResolveShortcut(InstalledApp app)
    {
        var path = app.LaunchPath;
        if (string.IsNullOrWhiteSpace(path) && app.FullName.StartsWith("StartMenu:", StringComparison.Ordinal))
            path = app.FullName[10..];
        return !string.IsNullOrWhiteSpace(path) && File.Exists(path) ? path : null;
    }
}
