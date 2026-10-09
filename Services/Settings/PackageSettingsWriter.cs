// SPDX-License-Identifier: GPL-3.0-only
using Windows.Management.Core;
using WinApps.Models;

namespace WinApps.Services.Settings;

public sealed class PackageSettingsWriter
{
    public SettingWriteResult Write(string familyName, AppSetting expected, string value)
    {
        if (string.IsNullOrWhiteSpace(familyName) || expected.Location is null)
            return new(false, "This setting has no editable package location.");
        try
        {
            var data = ApplicationDataManager.CreateForPackageFamily(familyName);
            var location = expected.Location;
            var container = location.Store switch
            {
                "LocalSettings" => data.LocalSettings,
                "RoamingSettings" => data.RoamingSettings,
                _ => throw new ArgumentException("Unknown settings store.")
            };
            foreach (var segment in location.Containers)
            {
                if (!container.Containers.TryGetValue(segment, out var child))
                    return new(false, "The settings container no longer exists. Reload settings before editing.");
                container = child;
            }
            return SettingUpdate.Apply(container.Values, expected, value);
        }
        catch (Exception ex) when (ex is System.Runtime.InteropServices.COMException or UnauthorizedAccessException
            or ArgumentException or InvalidOperationException or IOException or NotSupportedException)
        { return new(false, $"Windows could not save this setting: {ex.Message} (0x{ex.HResult:X8})."); }
    }
}
