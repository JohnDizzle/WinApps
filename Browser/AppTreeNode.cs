// SPDX-License-Identifier: GPL-3.0-only
using WinApps.Models;

namespace WinApps.Browser;

public sealed record AppTreeNode(string Key, string Label, InstalledApp? App, IReadOnlyList<AppTreeNode> Children, AppDataFile? File = null, string? Area = null);

