// SPDX-License-Identifier: GPL-3.0-only
using System.Text.Json;

namespace WinApps.Services.Storage;

public sealed record WindowPlacement(int X, int Y, int Width, int Height, bool Maximized = false);

public sealed class WindowPlacementStore(string? path = null)
{
    public const int MinimumWidth = 768;
    public const int MinimumHeight = 960;
    public const int DefaultWidth = 1440;
    public const int DefaultHeight = 960;
    private readonly string _path = path ?? Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WinApps", "window.json");

    public WindowPlacement? Load()
    {
        try
        {
            using var stream = File.OpenRead(_path);
            if (stream.Length > 4096) return null;
            var saved = JsonSerializer.Deserialize<WindowPlacement>(stream);
            return saved is { Width: > 0, Height: > 0 } ? saved : null;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException) { return null; }
    }

    public string? Save(WindowPlacement placement)
    {
        var temporary = _path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(_path))!);
            File.WriteAllText(temporary, JsonSerializer.Serialize(placement));
            File.Move(temporary, _path, overwrite: true);
            return null;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException) { return error.Message; }
        finally
        {
            try { File.Delete(temporary); }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException) { }
        }
    }

    public static WindowPlacement FirstRun(int left, int top, int width, int height, double scale = 1)
    {
        if (!double.IsFinite(scale) || scale <= 0 || scale > 8) scale = 1;
        var bounds = Fit(new(0, 0, (int)Math.Round(DefaultWidth * scale), (int)Math.Round(DefaultHeight * scale)),
            left, top, width, height);
        return bounds with { X = Math.Max(0, left) + (width - bounds.Width) / 2,
            Y = Math.Max(0, top) + (height - bounds.Height) / 2 };
    }

    public static WindowPlacement Fit(WindowPlacement saved, int left, int top, int width, int height)
    {
        if (width <= 0 || height <= 0) throw new ArgumentOutOfRangeException(nameof(width));
        left = Math.Max(0, left);
        top = Math.Max(0, top);
        var fittedWidth = Math.Clamp(saved.Width, Math.Min(MinimumWidth, width), width);
        var fittedHeight = Math.Clamp(saved.Height, Math.Min(MinimumHeight, height), height);
        return saved with
        {
            X = (int)Math.Clamp((long)saved.X, left, Math.Min(int.MaxValue, (long)left + width - fittedWidth)),
            Y = (int)Math.Clamp((long)saved.Y, top, Math.Min(int.MaxValue, (long)top + height - fittedHeight)),
            Width = fittedWidth, Height = fittedHeight
        };
    }
}
