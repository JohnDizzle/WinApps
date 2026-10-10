// SPDX-License-Identifier: GPL-3.0-only
using System.Text.Json;
using WinApps.Models;

namespace WinApps.Services.Inventory;

public sealed class ApplicationLibraryCache
{
    private readonly string path;
    public ApplicationLibraryCache(string? path = null) => this.path = path ?? Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WinApps", "library.json");

    public sealed record Snapshot(int SchemaVersion, DateTimeOffset SavedAt, InstalledApp[] Apps);

    public Snapshot? Load()
    {
        try
        {
            using var stream = File.OpenRead(path);
            if (stream.Length > 16 * 1024 * 1024) return null;
            var snapshot = JsonSerializer.Deserialize<Snapshot>(stream);
            return snapshot is { SchemaVersion: 4, Apps: not null } && snapshot.Apps.All(app =>
                app is not null && app.Name is not null && app.Publisher is not null &&
                app.FamilyName is not null && app.FullName is not null && app.DataPath is not null &&
                app.InstallPath is not null && app.Source is not null) ? snapshot : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException) { return null; }
    }

    public string? Save(IReadOnlyList<InstalledApp> apps)
    {
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
            File.WriteAllText(temporary, JsonSerializer.Serialize(new Snapshot(4, DateTimeOffset.Now, apps.ToArray())));
            File.Move(temporary, path, overwrite: true);
            return null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        { return $"Library cache could not be saved: {ex.Message}"; }
        finally
        {
            try { File.Delete(temporary); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        }
    }
}
