using System.Diagnostics;
using Windows.Management.Deployment;
using MyApps.Models;

namespace MyApps.Services.Packages;

public sealed class ApplicationLauncher
{
    public async Task<string> LaunchAsync(InstalledApp app, bool elevated)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(app.FamilyName)) return "No package launch identity is available for this application.";
            var package = new PackageManager().FindPackagesForUser(string.Empty).FirstOrDefault(item =>
                item.Id.FamilyName == app.FamilyName && item.InstalledLocation.Path.Equals(app.InstallPath, StringComparison.OrdinalIgnoreCase));
            if (package is null) return "Package registration changed. Refresh applications before launching.";
            if (!elevated)
            {
                var entries = await package.GetAppListEntriesAsync();
                var entry = entries.FirstOrDefault(item => item.AppUserModelId == app.FullName);
                if (entry is null) return "The selected application is no longer registered for launch.";
                return await entry.LaunchAsync() ? "Application launched." : "Windows could not launch this application.";
            }
            var id = app.FullName.Contains('!') ? app.FullName[(app.FullName.LastIndexOf('!') + 1)..] : "";
            var configuration = PackageManifestFile.Read(Path.Combine(package.InstalledLocation.Path, "AppxManifest.xml"), id);
            if (configuration.Launch is not { AllowsElevation: true } launch)
                return "This application's manifest does not declare supported elevation.";
            var executable = PackageLaunchPath.Resolve(package.InstalledLocation.Path, launch.Executable);
            if (!File.Exists(executable)) return "The declared executable is unavailable.";
            Process.Start(new ProcessStartInfo(executable) { UseShellExecute = true, Verb = "runas", WorkingDirectory = package.InstalledLocation.Path });
            return "Elevated launch requested.";
        }
        catch (System.ComponentModel.Win32Exception ex) when (ex.NativeErrorCode == 1223) { return "Elevated launch cancelled."; }
        catch (Exception ex) { return $"Launch failed: {ex.Message}"; }
    }
}
