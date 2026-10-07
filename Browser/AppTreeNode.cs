using MyApps.Models;

namespace MyApps.Browser;

public sealed record AppTreeNode(string Key, string Label, InstalledApp? App, IReadOnlyList<AppTreeNode> Children, AppDataFile? File = null, string? Area = null);

