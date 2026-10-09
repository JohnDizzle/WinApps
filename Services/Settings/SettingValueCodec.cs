// SPDX-License-Identifier: GPL-3.0-only
using System.Globalization;
using System.Text.Json;

namespace WinApps.Services.Settings;

public static class SettingValueCodec
{
    public static bool CanEdit(string type) => type is "String" or "Boolean" or "Byte" or "Int16" or "UInt16"
        or "Int32" or "UInt32" or "Int64" or "UInt64" or "Single" or "Double" or "Char" or "Guid"
        or "DateTimeOffset" or "TimeSpan";

    public static string Format(object? value) => value switch
    {
        null => "null", string item => item,
        DateTimeOffset date => date.ToString("O", CultureInfo.InvariantCulture),
        TimeSpan duration => duration.ToString("c", CultureInfo.InvariantCulture),
        Array array => JsonSerializer.Serialize(array, array.GetType()),
        IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString() ?? ""
    };

    public static bool TryParse(string type, string text, out object? value, out string error)
    {
        value = null;
        error = "";
        var culture = CultureInfo.InvariantCulture;
        try
        {
            value = type switch
            {
                "String" => text,
                "Boolean" => bool.Parse(text),
                "Byte" => byte.Parse(text, NumberStyles.Integer, culture),
                "Int16" => short.Parse(text, NumberStyles.Integer, culture),
                "UInt16" => ushort.Parse(text, NumberStyles.Integer, culture),
                "Int32" => int.Parse(text, NumberStyles.Integer, culture),
                "UInt32" => uint.Parse(text, NumberStyles.Integer, culture),
                "Int64" => long.Parse(text, NumberStyles.Integer, culture),
                "UInt64" => ulong.Parse(text, NumberStyles.Integer, culture),
                "Single" => float.Parse(text, NumberStyles.Float, culture),
                "Double" => double.Parse(text, NumberStyles.Float, culture),
                "Char" => char.Parse(text),
                "Guid" => Guid.Parse(text),
                "DateTimeOffset" => DateTimeOffset.ParseExact(text, "O", culture, DateTimeStyles.None),
                "TimeSpan" => TimeSpan.ParseExact(text, "c", culture),
                _ => throw new NotSupportedException("This value type is read only.")
            };
            if (value is float single && !float.IsFinite(single) || value is double number && !double.IsFinite(number))
                throw new FormatException("Enter a finite number.");
            return true;
        }
        catch (Exception ex) when (ex is FormatException or OverflowException or NotSupportedException)
        {
            value = null;
            error = $"Enter a valid {type} value. {ex.Message}";
            return false;
        }
    }
}
