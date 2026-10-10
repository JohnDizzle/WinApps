// SPDX-License-Identifier: GPL-3.0-only
namespace WinApps.Models;

public sealed record InstalledApp(string Name, string FamilyName, string FullName, string Publisher, string Version, string DataPath, string InstallPath, string Source = "MSIX", long? EstimatedSizeKiB = null, string? IconUri = null, string? ConsolePath = null, string? LaunchPath = null, string? ExecutablePath = null);
public sealed record PackageScan(IReadOnlyList<InstalledApp> Apps, int Skipped);

