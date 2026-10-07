// SPDX-License-Identifier: GPL-3.0-only
using System.Globalization;

namespace MyApps.Services.Settings;

public static class SettingNumericValue
{
    // NumberBox is backed by Double; these integer types need an exact editor.
    public static bool NeedsExactIntegerEditor(string type) => type is "Int64" or "UInt64";
    public static bool TryStep(string type, string text, int direction, out string next)
    {
        next = text;
        if (!NeedsExactIntegerEditor(type) || !SettingValueCodec.TryParse(type, text, out var parsed, out _)) return false;
        var number = Convert.ToDecimal(parsed, CultureInfo.InvariantCulture) + direction;
        var candidate = number.ToString(CultureInfo.InvariantCulture);
        if (!SettingValueCodec.TryParse(type, candidate, out _, out _)) return false;
        next = candidate;
        return true;
    }
}
