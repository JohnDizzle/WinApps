// SPDX-License-Identifier: GPL-3.0-only
namespace MyApps.Models;

public sealed record InstalledApp(string Name, string FamilyName, string FullName, string Publisher, string Version, string DataPath, string InstallPath, string Source = "MSIX", long? EstimatedSizeKiB = null, string? IconUri = null);
public sealed record PackageScan(IReadOnlyList<InstalledApp> Apps, int Skipped);



