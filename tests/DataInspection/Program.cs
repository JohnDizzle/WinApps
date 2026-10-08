// SPDX-License-Identifier: GPL-3.0-only
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using MyApps.Models;
using MyApps.Services.Storage;
using MyApps.Services.Inventory;
using MyApps.Services.Packages;
using System.Xml;
using MyApps.Services.Settings;

var root = Path.Combine(Path.GetTempPath(), "MyApps-inspection-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(root);
var inspector = new AppDataInspector();
var checks = 0;

void Check(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
    checks++;
}

string Preview(string name, string content, Encoding? encoding = null)
{
    var path = Path.Combine(root, name);
    File.WriteAllText(path, content, encoding ?? new UTF8Encoding(false));
    var before = File.ReadAllBytes(path);
    var info = new FileInfo(path);
    var result = inspector.Preview(new AppDataFile("LocalState", name, path, info.Length,
        info.LastWriteTimeUtc, AppDataInspector.Kind(path)));
    Check(before.SequenceEqual(File.ReadAllBytes(path)), $"Inspection modified {name}");
    return result;
}

try
{
    var notifications = new NotificationQueue();
    var noticeTime = DateTimeOffset.UtcNow;
    notifications.Add("First application finished", noticeTime);
    notifications.Add("Second application finished", noticeTime.AddSeconds(2));
    Check(notifications.Entries.Count == 2, "A new notification must retain earlier active messages.");
    notifications.Expire(noticeTime.AddSeconds(5));
    Check(notifications.Entries.Count == 1 && notifications.Entries[0].Message.StartsWith("Second"),
        "Earlier notification expiry must not dismiss a newer message.");
    notifications.Add("Same message", noticeTime.AddSeconds(5));
    notifications.Add("Same message", noticeTime.AddSeconds(5));
    Check(notifications.Entries.Select(entry => entry.Id).Distinct().Count() == 3,
        "Repeated notifications must retain distinct identities.");
    notifications.Dismiss(notifications.Entries[1].Id);
    Check(notifications.Entries.Count == 2, "Dismiss must remove only its own notification.");
    for (var n = 0; n < 8; n++) notifications.Add($"Notice {n}", noticeTime.AddSeconds(5));
    Check(notifications.Entries.Count == 4 && notifications.Entries[^1].Message == "Notice 7",
        "Notification stacks must remain bounded and retain the newest messages.");
    notifications.Expire(noticeTime.AddSeconds(10));
    Check(notifications.Entries.Count == 0, "Every notification must expire independently.");
    var placementPath = Path.Combine(root, "window.json");
    var placementStore = new WindowPlacementStore(placementPath);
    Check(placementStore.Load() is null, "Missing placement must use startup defaults.");
    Check(WindowPlacementStore.FirstRun(0, 0, 1920, 1080) == new WindowPlacement(240, 60, 1440, 960),
        "First-run defaults must be wider and centered.");
    var scaledFirstRun = WindowPlacementStore.FirstRun(0, 0, 2560, 1600, 1.5);
    Check(scaledFirstRun == new WindowPlacement(200, 80, 2160, 1440),
        "First-run defaults must account for display scaling so the rail remains visible.");
    Check(WindowPlacementStore.FirstRun(0, 0, 640, 480) == new WindowPlacement(0, 0, 640, 480),
        "First-run defaults must fit smaller displays.");
    File.WriteAllText(placementPath, "broken json");
    Check(placementStore.Load() is null, "Corrupt placement must be ignored.");
    var normalPlacement = new WindowPlacement(20, 40, 1200, 960, true);
    var placementSaveError = placementStore.Save(normalPlacement);
    Check(placementSaveError is null, $"Window placement save failed: {placementSaveError}");
    Check(placementStore.Load() == normalPlacement, "Normal bounds and maximized state must round-trip.");
    var smallPlacement = WindowPlacementStore.Fit(new(-100, -200, 1, 1), 0, 0, 1920, 1080);
    Check(smallPlacement == new WindowPlacement(0, 0, 768, 960), "Negative coordinates and undersized bounds must be corrected.");
    var distantPlacement = WindowPlacementStore.Fit(new(int.MaxValue, int.MaxValue, int.MaxValue, int.MaxValue), 0, 0, 1920, 1080);
    Check(distantPlacement == new WindowPlacement(0, 0, 1920, 1080), "Huge and disconnected-display bounds must fit without overflow.");
    var tinyDisplay = WindowPlacementStore.Fit(normalPlacement, 0, 0, 640, 480);
    Check(tinyDisplay.Width == 640 && tinyDisplay.Height == 480 && tinyDisplay.X == 0 && tinyDisplay.Y == 0,
        "A smaller display must keep the full window reachable.");
    var secondary = WindowPlacementStore.Fit(new(2200, 100, 800, 960), 1920, 0, 1920, 1080);
    Check(secondary.X == 2200 && secondary.Y == 100, "A valid positive secondary-display position must be retained.");
    File.WriteAllText(placementPath, "{\"Width\":-1,\"Height\":960}");
    Check(placementStore.Load() is null, "Invalid saved dimensions must be ignored.");
    using var canceled = new CancellationTokenSource();
    canceled.Cancel();
    var cancellationApp = new InstalledApp("Canceled", "family", "family!App", "Publisher", "1", root, root);
    var canceledInventory = inspector.Inspect(cancellationApp, canceled.Token);
    Check(canceledInventory.Canceled && canceledInventory.Files.Count == 0 && canceledInventory.Notices.Count == 0,
        "Canceled inventory must quietly stop without publishing partial files or error notices.");
    Check(new FootprintScanner().Scan(cancellationApp, canceled.Token).Count == 0,
        "Canceled storage measurement must quietly stop without publishing partial results.");
    Check(new FootprintScanner().Measure("Canceled", root, canceled.Token).State == "Canceled",
        "Canceled folder measurement must report cancellation without throwing.");
    var lockedRoot = Path.Combine(root, "locked-case");
    Directory.CreateDirectory(Path.Combine(lockedRoot, "LocalState"));
    var lockedPath = Path.Combine(lockedRoot, "LocalState", "locked.txt");
    var readablePath = Path.Combine(lockedRoot, "LocalState", "readable.txt");
    File.WriteAllText(lockedPath, "private locked contents");
    File.WriteAllText(readablePath, "readable contents");
    var lockedEntry = new AppDataFile("LocalState", "locked.txt", lockedPath, 0, DateTime.UtcNow, "Text / log");
    using (var lockedStream = new FileStream(lockedPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
    {
        Check(inspector.Preview(lockedEntry).StartsWith("Preview unavailable:"),
            "Exclusive file locks must produce a useful unavailable preview without throwing.");
        var lockedInventory = inspector.Inspect(cancellationApp with { DataPath = lockedRoot, InstallPath = "" });
        Check(lockedInventory.Files.Any(item => item.FullPath == readablePath),
            "A locked file must not stop inspection of other files.");
    }
    File.Delete(lockedPath);
    Check(inspector.Preview(lockedEntry).StartsWith("Preview unavailable:"),
        "A file deleted after inventory must produce an unavailable preview without throwing.");
    var location = new SettingLocation("LocalSettings", ["Nested/Name"], "MainWindowHeight");
    var height = new AppSetting("LocalSettings/Nested/Name", "MainWindowHeight", "Int32", "1048", location);
    var settingsMap = new Dictionary<string, object> { [height.Name] = 1048, ["Sibling"] = "untouched" };
    Check(SettingUpdate.Apply(settingsMap, height, "900").Saved && settingsMap[height.Name] is int savedHeight && savedHeight == 900,
        "A numeric setting must retain its Int32 storage type");
    Check((string)settingsMap["Sibling"] == "untouched", "Saving one setting must leave siblings unchanged");
    Check(!SettingUpdate.Apply(settingsMap, height, "800").Saved && (int)settingsMap[height.Name] == 900,
        "Stale snapshots must not overwrite newer values");
    var freshHeight = height with { Value = "900" };
    Check(!SettingUpdate.Apply(settingsMap, freshHeight, "2147483648").Saved && (int)settingsMap[height.Name] == 900,
        "Overflow must be rejected before any assignment");
    Check(!SettingUpdate.Apply(settingsMap, freshHeight, "900.5").Saved, "Fractional input must not change an integer setting");
    settingsMap[height.Name] = "900";
    Check(!SettingUpdate.Apply(settingsMap, freshHeight, "800").Saved, "Concurrent type changes must block saves");
    settingsMap.Remove(height.Name);
    Check(!SettingUpdate.Apply(settingsMap, freshHeight, "800").Saved && !settingsMap.ContainsKey(height.Name), "Removed settings must not be recreated");
    Check(!SettingUpdate.Apply(settingsMap, height with { Location = null }, "800").Saved, "Read-only members must not be written");
    foreach (var scalar in new object[] { "  unchanged string  ", true, (byte)255, short.MinValue, ushort.MaxValue,
        int.MinValue, uint.MaxValue, long.MinValue, ulong.MaxValue, 1.25f, 1.25d, 'A', Guid.NewGuid(),
        new DateTimeOffset(2026, 10, 6, 12, 30, 0, TimeSpan.FromHours(-4)), TimeSpan.FromMinutes(90) })
    {
        var type = scalar.GetType().Name;
        Check(SettingValueCodec.TryParse(type, SettingValueCodec.Format(scalar), out var roundtrip, out _) &&
            roundtrip?.GetType() == scalar.GetType() && Equals(roundtrip, scalar), $"{type} must round-trip without a type change");
    }
    foreach (var invalidSetting in new[] { ("Boolean", "yes"), ("Byte", "256"), ("UInt32", "-1"), ("Double", "NaN"),
        ("Single", "Infinity"), ("Guid", "not-a-guid"), ("Char", "AB"), ("Int32[]", "[1]"), ("Composite", "{}") })
        Check(!SettingValueCodec.TryParse(invalidSetting.Item1, invalidSetting.Item2, out _, out _), $"Invalid {invalidSetting.Item1} input should be rejected");
    var manifest = """
        <Package xmlns="http://schemas.microsoft.com/appx/manifest/foundation/windows10"
                 xmlns:modern="http://schemas.microsoft.com/appx/manifest/uap/windows10/10"
                 xmlns:uap="http://schemas.microsoft.com/appx/manifest/uap/windows10"
                 xmlns:rescap="http://schemas.microsoft.com/appx/manifest/foundation/windows10/restrictedcapabilities">
          <Identity Name="Example" Version="1.2.3.4" ProcessorArchitecture="x64" Publisher="CN=Example" />
          <Dependencies><TargetDeviceFamily Name="Windows.Desktop" MinVersion="10.0.19041.0" MaxVersionTested="10.0.22621.0" />
            <PackageDependency Name="Microsoft.WindowsAppRuntime.1.8" MinVersion="1.8.0.0" Publisher="CN=Microsoft" /></Dependencies>
          <Capabilities><Capability Name="internetClient" /><rescap:Capability Name="runFullTrust" /></Capabilities>
          <Applications>
            <Application Id="Desktop" Executable="desktop.exe" EntryPoint="windows.fullTrustApplication" />
            <Application Id="Uwp" Executable="uwp.exe" EntryPoint="Example.App" />
            <Application Id="Isolated" Executable="isolated.exe" modern:RuntimeBehavior="packagedClassicApp" modern:TrustLevel="appContainer">
              <Extensions><uap:Extension Category="windows.protocol"><uap:Protocol Name="example" /></uap:Extension></Extensions>
            </Application>
          </Applications>
        </Package>
        """;
    var parser = new PackageManifestInspector();
    var defaultVirtualization = parser.Parse(manifest, "Desktop");
    Check(defaultVirtualization.Sections.Any(item => item.Title == "Registry and filesystem virtualization" && item.Details.Contains("defaults apply")),
        "Absent virtualization overrides must not imply that no hive exists");
    var withVirtualization = manifest.Replace("<Dependencies>", "<Properties xmlns:d6=\"http://schemas.microsoft.com/appx/manifest/desktop/windows10/6\"><d6:RegistryWriteVirtualization>disabled</d6:RegistryWriteVirtualization></Properties><Dependencies>")
        .Replace("<rescap:Capability Name=\"runFullTrust\" />", "<rescap:Capability Name=\"unvirtualizedResources\" />");
    Check(parser.Parse(withVirtualization, "Desktop").Sections.Any(item => item.Title == "Registry and filesystem virtualization" && item.Details.Contains("disabled") && item.Details.Contains("declared: True")),
        "Declared virtualization overrides and capability must be reported");
    var sqlitePath = Path.Combine(root, "extensionless-store");
    File.WriteAllBytes(sqlitePath, "SQLite format 3\0"u8.ToArray());
    Check(AppDataInspector.Kind(sqlitePath).StartsWith("SQLite database"), "SQLite detection must not depend on a filename extension");
    Check(File.ReadAllBytes(sqlitePath).SequenceEqual("SQLite format 3\0"u8.ToArray()), "Signature inspection must not modify database files");
    var hivePath = Path.Combine(root, "User.dat");
    File.WriteAllBytes(hivePath, "regfsynthetic-test"u8.ToArray());
    Check(AppDataInspector.Kind(hivePath).StartsWith("Windows registry hive"), "Registry header identification must remain distinct from ApplicationData");
    Check(AppDataInspector.DataFolder(root, "Registry") == Path.Combine(root, "SystemAppData", "Helium"), "Registry stores must be discovered outside the Settings folder");
    Check(SettingNumericValue.TryStep("UInt64", "18446744073709551614", 1, out var exactInteger) && exactInteger == "18446744073709551615", "64-bit numeric stepping must preserve all digits");
    Check(!SettingNumericValue.TryStep("UInt64", "18446744073709551615", 1, out _), "Unsigned overflow must disable stepping");
    Check(!SettingNumericValue.TryStep("UInt64", "0", -1, out _), "Unsigned underflow must disable stepping");
    Check(SettingNumericValue.TryStep("Int64", "-9223372036854775808", 1, out var signed) && signed == "-9223372036854775807", "Signed 64-bit stepping must remain exact");
    Check(!SettingNumericValue.TryStep("Int64", "-9223372036854775808", -1, out _), "Signed underflow must disable stepping");
    Check(!SettingNumericValue.TryStep("Int64", "1.5", 1, out _), "Invalid integer drafts must not be rounded by stepping");
    var elevatedManifest = manifest.Replace("windows.fullTrustApplication", "Windows.FullTrustApplication")
        .Replace("windows10/10", "windows10/18")
        .Replace("<rescap:Capability Name=\"runFullTrust\" />", "<rescap:Capability Name=\"runFullTrust\" /><rescap:Capability Name=\"allowElevation\" />");
    var elevated = parser.Parse(elevatedManifest, "Desktop");
    Check(elevated.ApplicationType.StartsWith("Packaged desktop") && elevated.Launch is { AllowsElevation: true, Executable: "desktop.exe", ApplicationId: "Desktop" }, "Capitalized desktop entry point and elevation capability must be recognized");
    Check(!parser.Parse(elevatedManifest, "Uwp").Launch!.AllowsElevation, "A sibling UWP app must not receive desktop elevation");
    Check(!parser.Parse(elevatedManifest, "Isolated").Launch!.AllowsElevation, "Newer namespace AppContainer must not receive desktop elevation");
    Check(!parser.Parse(elevatedManifest.Replace("foundation/windows10/restrictedcapabilities", "untrusted/namespace"), "Desktop").Launch!.AllowsElevation, "Capability lookalikes must not enable elevation");
    Check(parser.Parse(elevatedManifest, "Missing").Launch is null, "Missing application must not inherit a sibling launch target");
    Check(PackageLaunchPath.Resolve(root, "bin/app.exe") == Path.Combine(root, "bin", "app.exe"), "Launch path must resolve within the package");
    foreach (var target in new[] { "../outside.exe", Path.Combine(root, "absolute.exe"), "app.exe:stream", "", "folder/file.txt" })
    {
        var rejected = false;
        try { PackageLaunchPath.Resolve(root, target); } catch (IOException) { rejected = true; }
        Check(rejected, "Invalid manifest executable paths must be rejected");
    }
    var desktop = parser.Parse(manifest, "Desktop");
    Check(desktop.ApplicationType.StartsWith("Packaged desktop") && desktop.TrustLevel.StartsWith("mediumIL"), "Full trust desktop must not be classified as UWP");
    Check(desktop.UiFramework.Contains("not confirmed"), "Windows App SDK dependency must not prove WinUI 3");
    Check(desktop.Manifest == manifest, "Complete manifest must retain original source");
    Check(desktop.Sections.Any(section => section.Details.Contains("runFullTrust")) && desktop.Sections.Any(section => section.Details.Contains("windows.protocol")), "Capabilities and nested extensions must be retained");
    var uwp = parser.Parse(manifest, "Uwp");
    Check(uwp.ApplicationType.StartsWith("UWP") && uwp.TrustLevel.StartsWith("appContainer"), "Legacy UWP activation should infer AppContainer");
    var isolated = parser.Parse(manifest, "Isolated");
    Check(isolated.ApplicationType.StartsWith("Packaged desktop") && isolated.TrustLevel == "appContainer · declared TrustLevel", "Namespace-aware desktop AppContainer declarations must be recognized");
    var missing = parser.Parse(manifest, "Missing");
    Check(missing.ApplicationType == "Unknown" && missing.Notices.Any(notice => notice.Contains("not found")), "Missing app id must not use a sibling application's classification");
    var partial = parser.Parse(manifest.Replace("windows.fullTrustApplication", "windows.partialTrustApplication"), "Desktop");
    Check(partial.TrustLevel.StartsWith("appContainer"), "Legacy partial trust desktop must infer AppContainer");
    var conflict = parser.Parse(manifest.Replace("modern:TrustLevel=\"appContainer\"", "modern:TrustLevel=\"appContainer\" EntryPoint=\"windows.fullTrustApplication\""), "Isolated");
    Check(conflict.Notices.Any(notice => notice.Contains("TrustLevel conflicts")), "Conflicting activation declarations should be reported");
    var win32 = parser.Parse(manifest.Replace("modern:RuntimeBehavior=\"packagedClassicApp\" modern:TrustLevel=\"appContainer\"", "modern:RuntimeBehavior=\"win32App\" modern:TrustLevel=\"mediumIL\""), "Isolated");
    Check(win32.ApplicationType.StartsWith("Win32") && win32.TrustLevel.StartsWith("mediumIL"), "Explicit Win32 activation should be recognized");
    var unsupported = parser.Parse(manifest.Replace("modern:RuntimeBehavior=\"packagedClassicApp\"", "modern:RuntimeBehavior=\"win32App\""), "Isolated");
    Check(unsupported.Notices.Any(notice => notice.Contains("not a supported combination")), "Unsupported isolation combinations should be reported");
    var noSdk = parser.Parse(manifest.Replace("Microsoft.WindowsAppRuntime.1.8", "Other.Framework"), "Desktop");
    Check(noSdk.UiFramework.StartsWith("UI framework unknown"), "Absence of an SDK dependency should not rule out self-contained WinUI");
    var resourceManifest = """
        <Package xmlns="http://schemas.microsoft.com/appx/manifest/foundation/windows10" xmlns:modern="http://schemas.microsoft.com/appx/manifest/uap/windows10/6">
          <Identity Name="Example" Publisher="CN=Example" Version="1.2.3.4" ProcessorArchitecture="neutral" ResourceId="ExtraLanguagesPack1" />
          <Properties><ResourcePackage>true</ResourcePackage><modern:AllowExecution>true</modern:AllowExecution></Properties>
          <Resources><Resource Language="fr-FR" /><Resource Language="de-DE" /><Resource Scale="200" /></Resources>
        </Package>
        """;
    var resource = parser.Parse(resourceManifest, "");
    Check(resource.ApplicationType.StartsWith("Resource package"), "Resource-only manifests should be recognized");
    Check(!resource.Notices.Any(notice => notice.Contains("not found")), "Resource-only manifests must not report a missing application id");
    Check(resource.Sections.Any(section => section.Details.Contains("fr-FR") && section.Details.Contains("Scale=\"200\"")), "Resource languages and scale declarations must be retained");
    Check(resource.Sections.Any(section => section.Details.Contains("AllowExecution")), "Resource execution declarations must remain visible");
    var resourceManifestPath = Path.Combine(root, "AppxManifest.xml");
    File.WriteAllText(resourceManifestPath, resourceManifest);
    Check(PackageManifestFile.Read(resourceManifestPath, "").Manifest == resourceManifest, "Resource file inspection must retain source");
    Check(File.ReadAllText(resourceManifestPath) == resourceManifest, "Resource manifest inspection must not modify its source");
    foreach (var bad in new[] { "<Package />", "<!DOCTYPE Package [<!ENTITY x SYSTEM 'file:///secret'>]>" + manifest,
        new string(' ', PackageManifestInspector.MaxManifestCharacters + 1), "<broken>" })
    {
        var rejected = false;
        try { parser.Parse(bad, "Desktop"); } catch (XmlException) { rejected = true; }
        Check(rejected, "Invalid, oversized, or DTD-containing manifest must be rejected");
    }
    var cachePath = Path.Combine(root, "library.json");
    var cache = new ApplicationLibraryCache(cachePath);
    Check(cache.Load() is null, "Missing library should trigger discovery");
    var app = new InstalledApp("Example", "family", "identity", "Publisher", "1", "data", "install");
    Check(cache.Save([app]) is null, "Library should save");
    Check(cache.Load()?.Apps.Single() == app, "Cached metadata should round-trip");
    Check(cache.Save([]) is null && cache.Load()?.Apps.Length == 0, "Empty library should remain valid");
    File.WriteAllText(cachePath, "broken");
    Check(cache.Load() is null, "Corrupt cache should trigger discovery");
    File.WriteAllText(cachePath, "{\"SchemaVersion\":2,\"Apps\":[]}");
    Check(cache.Load() is null, "Unsupported cache versions should trigger discovery");
    File.WriteAllText(cachePath, "{\"SchemaVersion\":1,\"Apps\":[null]}");
    Check(cache.Load() is null, "Invalid cache entries should trigger discovery");
    Check(cache.Save([app]) is null && cache.Load()?.Apps.Length == 1, "Refresh should replace invalid cache");
    var json = Preview("valid.json", "{\"items\":[1,true,null],\"name\":\"example\"}");
    using var document = JsonDocument.Parse(json);
    Check(json.Contains('\n') && document.RootElement.GetProperty("items").GetArrayLength() == 3,
        "JSON should format and retain values");
    var invalid = "{broken";
    Check(Preview("invalid.json", invalid).EndsWith(invalid), "Invalid JSON should retain source text");
    Check(Preview("deep.json", new string('[', 65) + "0" + new string(']', 65))
        .StartsWith("[JSON inspection unavailable:"), "JSON depth should be bounded");
    var xml = Preview("valid.xml", "<root><value id=\"1\">text</value></root>");
    Check(XDocument.Parse(xml).Root?.Element("value")?.Value == "text" && xml.Contains('\n'),
        "XML should format and retain content");
    var mixed = "<root>before <b>bold</b> after</root>";
    Check(Preview("mixed.config", mixed) == mixed, "Mixed content whitespace should be preserved");
    Check(Preview("invalid.xml", "<root>").StartsWith("[XML inspection unavailable:"),
        "Invalid XML should report a diagnostic");
    var entity = "<!DOCTYPE root [<!ENTITY external SYSTEM 'file:///missing-secret'>]><root>&external;</root>";
    Check(Preview("entity.xml", entity).StartsWith("[XML inspection unavailable:"),
        "XML DTDs must be prohibited");
    Check(Preview("utf16.json", "{\"name\":\"café\"}", Encoding.Unicode).Contains("name"),
        "BOM-encoded JSON should decode");
    var exact = "{}" + new string(' ', AppDataInspector.MaxPreviewBytes - 2);
    Check(Preview("exact.json", exact) == "{}", "Exactly 64 KiB should parse");
    Check(Preview("large.json", exact + " ").Contains("structured inspection skipped"),
        "Oversized input must not be parsed");
    Check(Preview("binary.txt", "hello\0world").StartsWith("Binary content detected"),
        "Binary text should not be displayed");
    Check(Preview("plain.txt", "original\r\ntext") == "original\r\ntext", "Plain text should remain raw");
    Console.WriteLine($"Passed {checks} data inspection checks.");
}
finally
{
    var lockedFixture = Path.Combine(root, "locked-case", "LocalState");
    if (Directory.Exists(lockedFixture))
    {
        foreach (var fixtureFile in Directory.GetFiles(lockedFixture)) File.Delete(fixtureFile);
        Directory.Delete(lockedFixture);
        Directory.Delete(Path.Combine(root, "locked-case"));
    }
    // Only remove the individual fixture files created by this test run.
    foreach (var file in Directory.GetFiles(root)) File.Delete(file);
    Directory.Delete(root);
}
