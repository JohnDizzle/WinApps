using Windows.Management.Core;
using Windows.Storage;
using MyApps.Models;

namespace MyApps.Services.Settings;

public sealed class PackageSettingsReader
{
    public AppSettingsSnapshot Read(string familyName)
    {
        var values = new List<AppSetting>();
        var notices = new List<string>();
        if (string.IsNullOrWhiteSpace(familyName))
            return new(values, ["ApplicationData settings are available for packaged apps. This desktop app has no package family."]);
        try
        {
            var data = ApplicationDataManager.CreateForPackageFamily(familyName);
            ReadContainer(data.LocalSettings, "LocalSettings", [], values, notices);
            ReadContainer(data.RoamingSettings, "RoamingSettings", [], values, notices);
        }
        catch (Exception ex) when (ex is System.Runtime.InteropServices.COMException or UnauthorizedAccessException or ArgumentException)
        { notices.Add($"Windows could not open settings for this package: {ex.Message} (0x{ex.HResult:X8})."); }
        return new(values.OrderBy(value => value.Container, StringComparer.OrdinalIgnoreCase)
            .ThenBy(value => value.Name, StringComparer.OrdinalIgnoreCase).ToArray(), notices);
    }

    private static void ReadContainer(ApplicationDataContainer container, string store, string[] containers, List<AppSetting> values, List<string> notices)
    {
        var path = string.Join("/", new[] { store }.Concat(containers));
        try
        {
            foreach (var pair in container.Values)
                ReadValue(path, pair.Key, pair.Value, values, new SettingLocation(store, containers, pair.Key));
            foreach (var child in container.Containers)
                ReadContainer(child.Value, store, [.. containers, child.Key], values, notices);
        }
        catch (Exception ex) when (ex is System.Runtime.InteropServices.COMException or UnauthorizedAccessException)
        { notices.Add($"Cannot read {path}: {ex.Message} (0x{ex.HResult:X8})."); }
    }

    private static void ReadValue(string path, string name, object? value, List<AppSetting> values, SettingLocation? location = null)
    {
        if (value is ApplicationDataCompositeValue composite)
        {
            values.Add(new(path, name, "Composite", $"{composite.Count} members"));
            foreach (var pair in composite) ReadValue($"{path}/{name} (composite)", pair.Key, pair.Value, values);
            return;
        }
        values.Add(new(path, name, value?.GetType().Name ?? "Null", SettingValueCodec.Format(value), location));
    }
}
