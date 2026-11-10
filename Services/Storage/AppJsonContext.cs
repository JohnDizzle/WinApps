// SPDX-License-Identifier: GPL-3.0-only
using System.Text.Json.Serialization;
using WinApps.Models;
using WinApps.Services.Inventory;

namespace WinApps.Services.Storage;

[JsonSerializable(typeof(WindowPlacement))]
[JsonSerializable(typeof(CompactWindowSettings))]
[JsonSerializable(typeof(ApplicationLibraryCache.Snapshot), TypeInfoPropertyName = "LibrarySnapshot")]
[JsonSerializable(typeof(ApplicationViewHistory.Snapshot), TypeInfoPropertyName = "HistorySnapshot")]
[JsonSerializable(typeof(SettingLocation))]
[JsonSerializable(typeof(System.Text.Json.JsonElement))]
[JsonSerializable(typeof(string[]))]
[JsonSerializable(typeof(bool[]))]
[JsonSerializable(typeof(byte[]))]
[JsonSerializable(typeof(short[]))]
[JsonSerializable(typeof(ushort[]))]
[JsonSerializable(typeof(int[]))]
[JsonSerializable(typeof(uint[]))]
[JsonSerializable(typeof(long[]))]
[JsonSerializable(typeof(ulong[]))]
[JsonSerializable(typeof(float[]))]
[JsonSerializable(typeof(double[]))]
[JsonSerializable(typeof(char[]))]
[JsonSerializable(typeof(Guid[]))]
[JsonSerializable(typeof(DateTimeOffset[]))]
[JsonSerializable(typeof(TimeSpan[]))]
internal partial class AppJsonContext : JsonSerializerContext;
