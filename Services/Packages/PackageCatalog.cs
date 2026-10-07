using Windows.Management.Deployment;
using MyApps.Models;

namespace MyApps.Services.Packages;

public sealed class PackageCatalog
{
    public PackageScan Scan()
    {
        var apps = new List<InstalledApp>();
        var skipped = 0;
        var manager = new PackageManager();
        foreach (var package in manager.FindPackagesForUser(string.Empty))
        {
            try
            {
                if (package.IsFramework || package.IsResourcePackage) continue;
                var entries = package.GetAppListEntriesAsync().AsTask().GetAwaiter().GetResult();
                if (entries.Count == 0) continue;
                var id = package.Id;
                var version = id.Version;
                foreach (var entry in entries) apps.Add(new InstalledApp(
                    string.IsNullOrWhiteSpace(entry.DisplayInfo.DisplayName) ? id.Name : entry.DisplayInfo.DisplayName,
                    id.FamilyName, entry.AppUserModelId, id.Publisher,
                    $"{version.Major}.{version.Minor}.{version.Build}.{version.Revision}",
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Packages", id.FamilyName), package.InstalledLocation.Path, IconUri: MyApps.Services.Icons.ApplicationIcons.PackageIcon(entry, $"{id.FullName}|{entry.AppUserModelId}")));
            }
            catch (Exception ex) when (ex is System.Runtime.InteropServices.COMException or UnauthorizedAccessException)
            {
                skipped++;
            }
        }
        return new PackageScan(apps.OrderBy(app => app.Name, StringComparer.OrdinalIgnoreCase).ToArray(), skipped);
    }
}




