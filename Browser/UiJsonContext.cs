// SPDX-License-Identifier: GPL-3.0-only
using System.Text.Json.Serialization;

namespace WinApps.Browser;

[JsonSerializable(typeof(UiResourceFile))]
internal partial class UiJsonContext : JsonSerializerContext;
