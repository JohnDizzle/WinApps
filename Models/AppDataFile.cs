// SPDX-License-Identifier: GPL-3.0-only
namespace WinApps.Models;

public sealed record AppDataFile(string Area, string RelativePath, string FullPath, long Bytes, DateTime LastWriteUtc, string Kind);
public sealed record AppDataInventory(IReadOnlyList<AppDataFile> Files, IReadOnlyList<string> Notices, bool Canceled = false);
