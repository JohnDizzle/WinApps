using System.Xml;
using MyApps.Models;

namespace MyApps.Services.Packages;

public static class PackageManifestFile
{
    public static PackageConfiguration Read(string path, string applicationId)
    {
        for (var current = path; current is not null; current = Path.GetDirectoryName(current))
            if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                throw new IOException("Manifest path contains a filesystem link.");
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        if (stream.Length > 2 * PackageManifestInspector.MaxManifestCharacters)
            throw new IOException("Manifest exceeds the supported byte limit.");
        using var reader = new StreamReader(stream);
        var buffer = new char[PackageManifestInspector.MaxManifestCharacters + 1];
        var count = reader.ReadBlock(buffer, 0, buffer.Length);
        return new PackageManifestInspector().Parse(new string(buffer, 0, count), applicationId, path);
    }
}
