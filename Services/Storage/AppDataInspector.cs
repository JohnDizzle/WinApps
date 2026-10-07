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
    public AppDataInventory Inspect(InstalledApp app)
    {
        var files = new List<AppDataFile>();
        var notices = new List<string>();
        if (string.IsNullOrWhiteSpace(app.DataPath))
            return new(files, ["Desktop app data locations require an app-specific adapter; no package data path is known."]);
        foreach (var area in new[] { "Settings", "LocalState" })
        {
            var root = Path.Combine(app.DataPath, area);
            var pending = new Stack<string>();
            pending.Push(root);
            while (pending.TryPop(out var folder))
            {
                try
                {
                    if ((File.GetAttributes(folder) & FileAttributes.ReparsePoint) != 0)
                    { notices.Add($"Link skipped: {folder}"); continue; }
                    foreach (var path in Directory.EnumerateFileSystemEntries(folder))
                    {
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

    public static string Kind(string path) => Path.GetFileName(path).Equals("settings.dat", StringComparison.OrdinalIgnoreCase)
        ? "Windows binary settings store"
        : Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".json" => "JSON configuration", ".xml" or ".config" => "XML / configuration",
            ".db" or ".sqlite" or ".sqlite3" => "Database (format unverified)",
            ".txt" or ".log" or ".ini" or ".yaml" or ".yml" or ".csv" => "Text / log",
            ".png" or ".jpg" or ".jpeg" or ".webp" => "Image", _ => "Binary / unknown"
        };

    public string Preview(AppDataFile file)
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
