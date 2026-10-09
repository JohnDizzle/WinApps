// SPDX-License-Identifier: GPL-3.0-only
using WinApps.Models;

namespace WinApps.Services.Storage;

public sealed class FootprintScanner
{
    public IReadOnlyList<FolderFootprint> Scan(InstalledApp app, CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested) return [];
        var results = new List<FolderFootprint> { Measure("Installed package", app.InstallPath, cancellationToken) };
        if (!string.IsNullOrWhiteSpace(app.DataPath)) foreach (var name in new[] { "Settings", "LocalState", "RoamingState", "LocalCache", "AC", "TempState" })
        {
            if (cancellationToken.IsCancellationRequested) return [];
            results.Add(Measure(name == "TempState" ? "TempState (temporary)" : name, Path.Combine(app.DataPath, name), cancellationToken));
        }
        if (!string.IsNullOrWhiteSpace(app.DataPath))
            results.Add(Measure("Virtual registry", AppDataInspector.DataFolder(app.DataPath, "Registry"), cancellationToken));
        return cancellationToken.IsCancellationRequested ? [] : results;
    }

    public FolderFootprint Measure(string label, string path, CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested) return new(label, path, 0, 0, 0, "Canceled");
        if (string.IsNullOrWhiteSpace(path)) return new(label, path, 0, 0, 0, "Location unknown");
        long bytes = 0, files = 0;
        var skipped = 0;
        var folders = new Stack<string>();
        folders.Push(path);
        try
        {
            var attributes = File.GetAttributes(path);
            if ((attributes & FileAttributes.ReparsePoint) != 0)
                return new(label, path, 0, 0, 1, "Link not followed");
        }
        catch (DirectoryNotFoundException) { return new(label, path, 0, 0, 0, "Not present"); }
        catch (FileNotFoundException) { return new(label, path, 0, 0, 0, "Not present"); }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
        { return new(label, path, 0, 0, 1, "Unavailable"); }

        while (folders.TryPop(out var folder))
        {
            if (cancellationToken.IsCancellationRequested) return new(label, path, 0, 0, 0, "Canceled");
            try
            {
                foreach (var entry in Directory.EnumerateFileSystemEntries(folder))
                {
                    if (cancellationToken.IsCancellationRequested) return new(label, path, 0, 0, 0, "Canceled");
                    try
                    {
                        var attributes = File.GetAttributes(entry);
                        if ((attributes & FileAttributes.ReparsePoint) != 0) { skipped++; continue; }
                        if ((attributes & FileAttributes.Directory) != 0) folders.Push(entry);
                        else { bytes += new FileInfo(entry).Length; files++; }
                    }
                    catch (Exception ex) when (ex is UnauthorizedAccessException or IOException) { skipped++; }
                }
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or IOException) { skipped++; }
        }
        return new(label, path, bytes, files, skipped, skipped == 0 ? "Measured" : "Partial");
    }
}
