using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using Windows.ApplicationModel.Core;
using Windows.Foundation;

namespace MyApps.Services.Icons;

public static class ApplicationIcons
{
    private static string CachePath(string key)
    {
        var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MyApps", "IconCache");
        Directory.CreateDirectory(folder);
        return Path.Combine(folder, Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key))) + ".png");
    }

    public static string? PackageIcon(AppListEntry entry, string identity)
    {
        try
        {
            var path = CachePath(identity);
            if (!File.Exists(path))
            {
                using var stream = entry.DisplayInfo.GetLogo(new Windows.Foundation.Size(64, 64)).OpenReadAsync().AsTask().GetAwaiter().GetResult();
                using var input = stream.AsStreamForRead();
                using var output = File.Create(path);
                input.CopyTo(output);
            }
            return new Uri(path).AbsoluteUri;
        }
        catch { return null; }
    }

    public static string? DesktopIcon(string? displayIcon)
    {
        if (string.IsNullOrWhiteSpace(displayIcon)) return null;
        var path = displayIcon.Trim();
        var index = 0;
        var comma = path.LastIndexOf(',');
        if (comma >= 0 && int.TryParse(path[(comma + 1)..].Trim(), out var parsed))
        { index = parsed; path = path[..comma]; }
        path = Environment.ExpandEnvironmentVariables(path.Trim().Trim('"'));
        if (!Path.IsPathFullyQualified(path) || !File.Exists(path)) return null;
        nint large = 0, small = 0;
        try
        {
            var cached = CachePath($"{path}|{index}|{File.GetLastWriteTimeUtc(path).Ticks}");
            if (!File.Exists(cached))
            {
                if (ExtractIconEx(path, index, out large, out small, 1) == 0) return null;
                var handle = large != 0 ? large : small;
                if (handle == 0) return null;
                using var icon = System.Drawing.Icon.FromHandle(handle);
                using var bitmap = icon.ToBitmap();
                bitmap.Save(cached, ImageFormat.Png);
            }
            return new Uri(cached).AbsoluteUri;
        }
        catch { return null; }
        finally
        {
            if (large != 0) DestroyIcon(large);
            if (small != 0) DestroyIcon(small);
        }
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern uint ExtractIconEx(string file, int index, out nint large, out nint small, uint count);
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyIcon(nint icon);
}

