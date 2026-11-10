// SPDX-License-Identifier: GPL-3.0-only
using System.Runtime.CompilerServices;
using System.Text.Json;
using WinApps.Services.Inventory;
using WinApps.Services.Packages;
using WinApps.Services.Settings;
using WinApps.Services.Storage;

namespace WinApps.Application;

internal static class AotSelfCheck
{
    internal static void Run()
    {
        if (RuntimeFeature.IsDynamicCodeSupported || RuntimeFeature.IsDynamicCodeCompiled)
            throw new InvalidOperationException("This executable is not running as Native AOT.");
        var placement = new WindowPlacement(10, 20, 1000, 800);
        var json = JsonSerializer.Serialize(placement, AppJsonContext.Default.WindowPlacement);
        if (JsonSerializer.Deserialize(json, AppJsonContext.Default.WindowPlacement) != placement ||
            SettingValueCodec.Format(new[] { 1, 2 }) != "[1,2]")
            throw new InvalidOperationException("Source-generated JSON round-trip failed.");
        var directory = Path.Combine(Path.GetTempPath(), "WinApps-aot-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var shortcut = Path.Combine(directory, "fixture.lnk");
        try
        {
            using (var link = new ShellShortcut())
            {
                link.TargetPath = Environment.ProcessPath!;
                link.Arguments = "--native-fixture";
                link.Description = "Native AOT interop check";
                link.Save(shortcut);
            }
            using (var link = new ShellShortcut(shortcut))
                if (link.Arguments != "--native-fixture" || !link.TargetPath.Equals(Environment.ProcessPath, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("Native Shell shortcut round-trip failed.");
            var count = 0;
            NativeWindowEnumeration.All(_ => { count++; return true; });
            Console.WriteLine($"Native AOT verified; JSON, Shell shortcut round-trip, and native window callbacks passed ({count} windows enumerated).");
        }
        finally { if (File.Exists(shortcut)) File.Delete(shortcut); Directory.Delete(directory); }
    }
}
