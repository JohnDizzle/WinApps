using System.Xml;
using Windows.Management.Deployment;
using MyApps.Models;

namespace MyApps.Services.Packages;

public sealed class PackageConfigurationReader
{
    public PackageConfiguration Read(InstalledApp app)
    {
        if (string.IsNullOrWhiteSpace(app.FamilyName))
            return new("", "Desktop registration (no known MSIX identity)", "Unknown", "Unknown", [], "",
                ["This application has no known package family; an MSIX manifest is not available."]);
        var path = Path.Combine(app.InstallPath, "AppxManifest.xml");
        var sections = new List<PackageConfigurationSection>();
        var notices = new List<string>();
        var resources = new List<ResourcePackageInfo>();
        var resourceQueryCompleted = false;
        try
        {
            var manager = new PackageManager();
            foreach (var resource in manager.FindPackagesForUserWithPackageTypes(string.Empty, app.FamilyName, PackageTypes.Resource)
                .OrderBy(item => item.Id.FullName, StringComparer.OrdinalIgnoreCase))
            {
                if (!resource.IsResourcePackage || !resource.Id.FamilyName.Equals(app.FamilyName, StringComparison.OrdinalIgnoreCase)) continue;
                if (resources.Count >= 128)
                {
                    notices.Add("Resource package inspection limited to 128 packages.");
                    break;
                }
                try
                {
                    var id = resource.Id;
                    var version = id.Version;
                    var versionText = $"{version.Major}.{version.Minor}.{version.Build}.{version.Revision}";
                    var resourcePath = Path.Combine(resource.InstalledLocation.Path, "AppxManifest.xml");
                    var registration = $"Relationship: installed resource package in the same package family\nFamily: {id.FamilyName}\nFull name: {id.FullName}\nResource Id: {id.ResourceId}\nVersion: {versionText}\nMatches selected app version: {versionText == app.Version}\nArchitecture: {id.Architecture}\nPublisher: {id.Publisher}\nSignature kind: {resource.SignatureKind}\nPackage status OK: {resource.Status.VerifyIsOK()}\nInstalled location: {resource.InstalledLocation.Path}";
                    PackageConfiguration? manifest = null;
                    var resourceNotices = new List<string>();
                    try { manifest = PackageManifestFile.Read(resourcePath, ""); }
                    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or XmlException or InvalidOperationException)
                    { resourceNotices.Add($"Resource manifest unavailable: {ex.Message}"); }
                    resources.Add(new(id.FullName, id.FamilyName, id.ResourceId, versionText, id.Architecture.ToString(),
                        resource.InstalledLocation.Path, registration, resourcePath, manifest?.Manifest ?? "",
                        manifest?.Sections ?? [], resourceNotices.Concat(manifest?.Notices ?? []).ToArray()));
                }
                catch (Exception ex) when (ex is System.Runtime.InteropServices.COMException or IOException or UnauthorizedAccessException)
                { notices.Add($"One related resource package could not be inspected: {ex.Message}"); }
            }
            resourceQueryCompleted = true;
        }
        catch (Exception ex) when (ex is System.Runtime.InteropServices.COMException or UnauthorizedAccessException or IOException)
        { notices.Add($"Resource package discovery unavailable: {ex.Message}"); }
        try
        {
            var package = new PackageManager().FindPackagesForUser(string.Empty).FirstOrDefault(item =>
                item.Id.FamilyName == app.FamilyName && item.InstalledLocation.Path.Equals(app.InstallPath, StringComparison.OrdinalIgnoreCase));
            if (package is null) notices.Add("No matching current-user package registration. Refresh the library if the application has changed.");
            else
            {
                sections.Add(new("Windows package registration", $"Name: {package.Id.Name}\nFull name: {package.Id.FullName}\nFamily: {package.Id.FamilyName}\nPublisher: {package.Id.Publisher}\nPublisher Id: {package.Id.PublisherId}\nArchitecture: {package.Id.Architecture}\nResource Id: {package.Id.ResourceId}\nSignature kind: {package.SignatureKind}\nDevelopment mode: {package.IsDevelopmentMode}\nFramework: {package.IsFramework}\nResource package: {package.IsResourcePackage}\nBundle: {package.IsBundle}\nPackage status OK: {package.Status.VerifyIsOK()}"));
                sections.Add(new("Resolved installed dependencies", string.Join("\n", package.Dependencies.Select(dependency => dependency.Id.FullName))));
                var status = package.Status;
                sections.Add(new("Package health and deployment status", $"Dependency issue: {status.DependencyIssue}\nLicense issue: {status.LicenseIssue}\nModified: {status.Modified}\nTampered: {status.Tampered}\nDisabled: {status.Disabled}\nNeeds remediation: {status.NeedsRemediation}\nNot available: {status.NotAvailable}\nPackage offline: {status.PackageOffline}\nData offline: {status.DataOffline}\nDeployment in progress: {status.DeploymentInProgress}"));
                sections.Add(new("Installed package metadata", $"Display name: {package.DisplayName}\nPublisher display name: {package.PublisherDisplayName}\nDescription: {package.Description}\nInstalled location: {package.InstalledLocation.Path}\nInstalled date: {package.InstalledDate:O}\nOptional package: {package.IsOptional}\nStub package: {(OperatingSystem.IsWindowsVersionAtLeast(10, 0, 19041) ? package.IsStub.ToString() : "Unavailable on this Windows version")}"));
            }
        }
        catch (Exception ex) when (ex is System.Runtime.InteropServices.COMException or UnauthorizedAccessException or IOException)
        { notices.Add($"Package registration unavailable: {ex.Message}"); }
        try
        {
            if (string.IsNullOrWhiteSpace(app.InstallPath)) throw new IOException("Installed location is unknown.");
            var id = app.FullName.Contains('!') ? app.FullName[(app.FullName.LastIndexOf('!') + 1)..] : "";
            var result = PackageManifestFile.Read(path, id);
            return result with { Sections = sections.Concat(result.Sections).ToArray(), Notices = notices.Concat(result.Notices).ToArray(),
                ResourcePackages = resources, ResourceQueryCompleted = resourceQueryCompleted };
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or XmlException or InvalidOperationException)
        {
            notices.Add($"Manifest unavailable: {ex.Message}");
            return new(path, "Unknown", "Unknown", "Unknown", sections, "", notices, resources, resourceQueryCompleted);
        }
    }
}
