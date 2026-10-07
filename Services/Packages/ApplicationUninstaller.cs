// SPDX-License-Identifier: GPL-3.0-only
using Windows.Management.Deployment;
using MyApps.Models;

namespace MyApps.Services.Packages;

public sealed class ApplicationUninstaller
{
    public async Task<string> UninstallAsync(InstalledApp app)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(app.FamilyName))
                return "Use Windows Installed apps to uninstall this desktop application.";
            var manager = new PackageManager();
            var package = manager.FindPackagesForUser(string.Empty).FirstOrDefault(item =>
                item.Id.FamilyName == app.FamilyName && !item.IsResourcePackage && !item.IsFramework &&
                item.InstalledLocation.Path.Equals(app.InstallPath, StringComparison.OrdinalIgnoreCase));
            if (package is null) return "Package registration changed. Refresh applications before uninstalling.";
            if (!(await package.GetAppListEntriesAsync()).Any(entry => entry.AppUserModelId == app.FullName))
                return "The selected application is no longer registered. Refresh applications.";
            var result = await manager.RemovePackageAsync(package.Id.FullName);
            return result.ExtendedErrorCode is { HResult: < 0 } error
                ? $"Uninstall failed (0x{error.HResult:X8}): {result.ErrorText}"
                : "Application uninstalled for this user. Refresh applications to update the library.";
        }
        catch (Exception error) { return $"Uninstall failed: {error.Message}"; }
    }
}
