// SPDX-License-Identifier: GPL-3.0-only
using MyApps.Models;

namespace MyApps.Services.Settings;

public static class SettingUpdate
{
    public static SettingWriteResult Apply(IDictionary<string, object> values, AppSetting expected, string text)
    {
        if (!SettingValueCodec.CanEdit(expected.Type) || expected.Location is null)
            return new(false, "This setting is read only; composite members and arrays are not edited.");
        if (!SettingValueCodec.TryParse(expected.Type, text, out var replacement, out var error))
            return new(false, error);
        if (!values.TryGetValue(expected.Location.Key, out var current) || current is null)
            return new(false, "The setting no longer exists. Reload settings before editing it.");
        if (current.GetType().Name != expected.Type || SettingValueCodec.Format(current) != expected.Value)
            return new(false, "This setting changed since you began editing. Reload settings and review the new value.");
        values[expected.Location.Key] = replacement!;
        if (!values.TryGetValue(expected.Location.Key, out var saved) || saved?.GetType() != replacement!.GetType() || !Equals(saved, replacement))
            return new(true, "Windows accepted the save, but the read-back value differs. Reload settings to review it.");
        return new(true, $"Saved {expected.Name} as {expected.Type}.");
    }
}
