using MyApps.Models;
using MyApps.Services.Packages;
using Windows.Management.Deployment;

var manager = new PackageManager();
var tested = 0;
var totalResources = 0;
var names = args.Length > 0 ? args : new[] { "VioletHansen.AppControlManager", "Microsoft.WinUI3ControlsGallery" };
foreach (var name in names)
{
    var package = manager.FindPackagesForUserWithPackageTypes(string.Empty, PackageTypes.Main)
        .FirstOrDefault(item => item.Id.Name == name);
    if (package is null) { Console.WriteLine($"Skipped {name}: not installed for this user."); continue; }
    var entries = await package.GetAppListEntriesAsync();
    var entry = entries.FirstOrDefault();
    if (entry is null) throw new InvalidOperationException($"{name}: no application entry available.");
    var version = package.Id.Version;
    var app = new InstalledApp(name, package.Id.FamilyName, entry.AppUserModelId, package.Id.Publisher,
        $"{version.Major}.{version.Minor}.{version.Build}.{version.Revision}", "", package.InstalledLocation.Path);
    var result = new PackageConfigurationReader().Read(app);
    if (result.Manifest.Length == 0 || !result.ResourceQueryCompleted)
        throw new InvalidOperationException($"{name}: inspection failed: {string.Join("; ", result.Notices)}");
    if (result.Launch is null || result.Launch.ApplicationId != entry.AppUserModelId.Split('!').Last())
        throw new InvalidOperationException($"{name}: selected application launch metadata is missing.");
    if (name == "VioletHansen.AppControlManager" &&
        (!result.ApplicationType.StartsWith("Packaged desktop") || !result.Launch.AllowsElevation))
        throw new InvalidOperationException("AppControl Manager's declared desktop runtime and elevation must be recognized.");
    var expected = manager.FindPackagesForUserWithPackageTypes(string.Empty, app.FamilyName, PackageTypes.Resource)
        .Where(item => item.IsResourcePackage).Select(item => item.Id.FullName).Order().ToArray();
    var resources = result.ResourcePackages ?? [];
    if (!resources.Select(item => item.FullName).Order().SequenceEqual(expected))
        throw new InvalidOperationException($"{name}: related resource set does not match Windows registration.");
    foreach (var resource in resources)
        if (resource.FamilyName != app.FamilyName || resource.Manifest.Length == 0 || resource.Notices.Any(notice => notice.Contains("not found")))
            throw new InvalidOperationException($"{name}: resource inspection failed for {resource.FullName}.");
    totalResources += resources.Count;
    tested++;
    Console.WriteLine($"Passed {name}: main manifest and {resources.Count} resource manifests read; {result.ApplicationType}; elevation declared: {result.Launch.AllowsElevation}.");
}
if (tested == 0) throw new InvalidOperationException("No installed packages tested. Pass installed package names as arguments.");
Console.WriteLine($"Passed installed-package inspection for {tested} apps and {totalResources} resource packs.");
