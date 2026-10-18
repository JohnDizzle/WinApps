// SPDX-License-Identifier: GPL-3.0-only
using System.Text.Json;

namespace WinApps.Services.Storage;

public sealed record CompactWindowSettings(bool RememberPrevious = true, WindowPlacement? Placement = null, bool StartInCompactMode = false);

public sealed class CompactWindowStore(string? path = null)
{
    public static CompactWindowStore Shared { get; } = new();
    private readonly string _path = path ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "WinApps", "compact-window.json");
    public CompactWindowSettings Load()
    {
        try
        {
            using var stream = File.OpenRead(_path);
            if (stream.Length > 4096) return new();
            var saved = JsonSerializer.Deserialize<CompactWindowSettings>(stream) ?? new();
            return saved.Placement is { Width: <= 0 } or { Height: <= 0 } ? saved with { Placement = null } : saved;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException) { return new(); }
    }
    public string? SetStartInCompactMode(bool enabled) => Save(Load() with { StartInCompactMode = enabled });
    public string? SetRememberPrevious(bool remember) => Save(Load() with { RememberPrevious = remember });
    public string? SavePlacement(WindowPlacement placement)
    {
        var settings = Load();
        return settings.RememberPrevious ? Save(settings with { Placement = placement with { Maximized = false } }) : null;
    }
    private string? Save(CompactWindowSettings settings)
    {
        var temporary = _path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(_path))!);
            File.WriteAllText(temporary, JsonSerializer.Serialize(settings));
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
    public static WindowPlacement Fit(WindowPlacement saved, int left, int top, int width, int height)
    {
        if (width <= 0 || height <= 0) throw new ArgumentOutOfRangeException(nameof(width));
        var fittedWidth = Math.Clamp(saved.Width, Math.Min(360, width), width);
        var fittedHeight = Math.Clamp(saved.Height, Math.Min(240, height), height);
        return saved with
        {
            X = (int)Math.Clamp((long)saved.X, left, Math.Min(int.MaxValue, (long)left + width - fittedWidth)),
            Y = (int)Math.Clamp((long)saved.Y, top, Math.Min(int.MaxValue, (long)top + height - fittedHeight)),
            Width = fittedWidth, Height = fittedHeight, Maximized = false
        };
    }
}
