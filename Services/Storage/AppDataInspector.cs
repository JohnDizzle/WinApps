// SPDX-License-Identifier: GPL-3.0-only
using System.Text;
using System.Text.Json;
using System.Xml;
using System.Xml.Linq;
using MyApps.Models;

namespace MyApps.Services.Storage;

public sealed class AppDataInspector
{
    public const int MaxFiles = 2000;
    public const int MaxPreviewBytes = 65536;
    public AppDataInventory Inspect(InstalledApp app, CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested) return new([], [], Canceled: true);
        var files = new List<AppDataFile>();
        var notices = new List<string>();
        if (string.IsNullOrWhiteSpace(app.DataPath))
            return new(files, ["Desktop app data locations require an app-specific adapter; no package data path is known."]);
        if (!string.IsNullOrWhiteSpace(app.FamilyName) && !string.IsNullOrWhiteSpace(app.InstallPath))
            foreach (var name in new[] { "registry.dat", "user.dat", "userclasses.dat" })
            {
                if (cancellationToken.IsCancellationRequested) return new([], [], Canceled: true);
                var path = Path.Combine(app.InstallPath, name);
                try
                {
                    if (!File.Exists(path)) continue;
                    if (ContainsLink(path)) { notices.Add($"Link skipped: {path}"); continue; }
                    var info = new FileInfo(path);
                    if (files.Count >= MaxFiles) break;
                    files.Add(new("Registry", $"Package/{name}", path, info.Length, info.LastWriteTimeUtc,
                        "Packaged registry baseline · not decoded; separate from per-user changes"));
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                { notices.Add($"Unavailable registry baseline: {path} ({ex.Message})"); }
            }
        // Registry stores are independent of Windows ApplicationData settings.
        foreach (var area in new[] { "Settings", "Registry", "LocalState" })
        {
            var root = DataFolder(app.DataPath, area);
            var pending = new Stack<string>();
            pending.Push(root);
            while (pending.TryPop(out var folder))
            {
                if (cancellationToken.IsCancellationRequested) return new([], [], Canceled: true);
                try
                {
                    if (ContainsLink(folder))
                    { notices.Add($"Link skipped: {folder}"); continue; }
                    foreach (var path in Directory.EnumerateFileSystemEntries(folder))
                    {
                        if (cancellationToken.IsCancellationRequested) return new([], [], Canceled: true);
                        try
                        {
                            var attrs = File.GetAttributes(path);
                            if ((attrs & FileAttributes.ReparsePoint) != 0) { notices.Add($"Link skipped: {path}"); continue; }
                            if ((attrs & FileAttributes.Directory) != 0) { pending.Push(path); continue; }
                            var file = new FileInfo(path);
                            files.Add(new(area, Path.GetRelativePath(root, path), path, file.Length, file.LastWriteTimeUtc, Kind(path)));
                            if (files.Count >= MaxFiles)
                            {
                                notices.Add($"Inventory limited to {MaxFiles} files. Open the folder to inspect remaining files.");
                                return new(files, notices);
                            }
                        }
                        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                        { notices.Add($"Unavailable: {path} ({ex.Message})"); }
                    }
                }
                catch (DirectoryNotFoundException) { notices.Add($"{area}: folder not present ({folder})."); }
                catch (FileNotFoundException) { notices.Add($"{area}: folder not present ({folder})."); }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                { notices.Add($"Unavailable: {folder} ({ex.Message})"); }
            }
        }
        return new(files.OrderBy(f => f.Area).ThenBy(f => f.RelativePath, StringComparer.OrdinalIgnoreCase).ToArray(), notices);
    }

    public static string DataFolder(string dataPath, string area) => area == "Registry"
        ? Path.Combine(dataPath, "SystemAppData", "Helium") : Path.Combine(dataPath, area);

    private static bool ContainsLink(string path)
    {
        for (var current = path; current is not null; current = Path.GetDirectoryName(current))
            if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0) return true;
        return false;
    }

    public static string Kind(string path)
    {
        try
        {
            if (File.Exists(path) && !ContainsLink(path))
            {
                using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                Span<byte> header = stackalloc byte[16];
                var read = stream.ReadAtLeast(header, header.Length, throwOnEndOfStream: false);
                if (read == 16 && header.SequenceEqual("SQLite format 3\0"u8))
                    return "SQLite database · header identified; tables not inspected";
                if (read >= 4 && header[..4].SequenceEqual("regf"u8))
                    return Path.GetFileName(path).Equals("settings.dat", StringComparison.OrdinalIgnoreCase)
                        ? "Windows ApplicationData settings store · decoded through Windows"
                        : "Windows registry hive · not decoded";
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { /* Metadata still remains available. */ }
        if (Path.GetFileName(path).ToLowerInvariant() is "user.dat" or "userclasses.dat" or "registry.dat")
            return "Registry hive candidate · format unverified; not decoded";
        return Path.GetFileName(path).Equals("settings.dat", StringComparison.OrdinalIgnoreCase)
        ? "Windows binary settings store"
        : Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".json" => "JSON configuration", ".xml" or ".config" => "XML / configuration",
            ".db" or ".db3" or ".sqlite" or ".sqlite3" or ".mdb" or ".accdb" or ".edb" => "Database (format unverified)",
            ".txt" or ".log" or ".ini" or ".yaml" or ".yml" or ".csv" => "Text / log",
            ".png" or ".jpg" or ".jpeg" or ".webp" => "Image", _ => "Binary / unknown"
        };
    }

    public string Preview(AppDataFile file)
    {
        try { return ReadPreview(file); }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or
            System.Security.SecurityException or ArgumentException or NotSupportedException)
        {
            return "Preview unavailable: the file is locked, missing, or inaccessible. Other files can still be inspected.";
        }
    }

    private static string ReadPreview(AppDataFile file)
    {
        var extension = Path.GetExtension(file.FullPath).ToLowerInvariant();
        if (extension is not (".json" or ".xml" or ".config" or ".txt" or ".log" or ".ini" or ".yaml" or ".yml" or ".csv"))
            return $"{file.Kind}. Text preview is unavailable. The file remains unchanged.";
        // Re-check every ancestor: never traverse a reparse point during preview.
        for (var current = file.FullPath; current is not null; current = Path.GetDirectoryName(current))
            if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                return "Preview unavailable: path contains a filesystem link.";
        using var stream = new FileStream(file.FullPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        var bytes = new byte[MaxPreviewBytes];
        var read = stream.ReadAtLeast(bytes, bytes.Length, throwOnEndOfStream: false);
        var truncated = stream.ReadByte() != -1;
        using var reader = new StreamReader(new MemoryStream(bytes, 0, read), Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        var text = reader.ReadToEnd();
        if (text.Contains('\0')) return "Binary content detected; text preview unavailable.";
        if (truncated) return text + "\n\n[Preview truncated at 64 KiB; structured inspection skipped]";
        return FormatStructuredText(text, extension);
    }

    private static string FormatStructuredText(string text, string extension)
    {
        try
        {
            if (extension == ".json")
            {
                using var document = JsonDocument.Parse(text, new JsonDocumentOptions { MaxDepth = 64 });
                return JsonSerializer.Serialize(document.RootElement, new JsonSerializerOptions { WriteIndented = true });
            }
            if (extension is ".xml" or ".config")
            {
                var options = new XmlReaderSettings
                {
                    DtdProcessing = DtdProcessing.Prohibit,
                    XmlResolver = null,
                    MaxCharactersInDocument = MaxPreviewBytes
                };
                using var reader = XmlReader.Create(new StringReader(text), options);
                var document = XDocument.Load(reader, LoadOptions.PreserveWhitespace);
                // Mixed content and existing whitespace must retain their meaning.
                return document.ToString();
            }
        }
        catch (JsonException ex)
        {
            return $"[JSON inspection unavailable: {ex.Message}]\n\n{text}";
        }
        catch (XmlException ex)
        {
            return $"[XML inspection unavailable: {ex.Message}]\n\n{text}";
        }
        return text;
    }
}
