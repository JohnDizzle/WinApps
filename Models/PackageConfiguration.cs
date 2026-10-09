// SPDX-License-Identifier: GPL-3.0-only
namespace WinApps.Models;

public sealed record PackageConfigurationSection(string Title, string Details);
public sealed record PackageConfiguration(string ManifestPath, string ApplicationType, string TrustLevel,
    string UiFramework, IReadOnlyList<PackageConfigurationSection> Sections, string Manifest,
    IReadOnlyList<string> Notices, IReadOnlyList<ResourcePackageInfo>? ResourcePackages = null,
    bool ResourceQueryCompleted = false, PackageLaunchInfo? Launch = null);

public sealed record PackageLaunchInfo(string ApplicationId, string Executable, bool AllowsElevation);

public sealed record ResourcePackageInfo(string FullName, string FamilyName, string ResourceId,
    string Version, string Architecture, string InstallPath, string Registration,
    string ManifestPath, string Manifest, IReadOnlyList<PackageConfigurationSection> Sections,
    IReadOnlyList<string> Notices);
