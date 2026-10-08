// SPDX-License-Identifier: GPL-3.0-only
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
    public static string NormalizeCachedPackageIcon(string iconUri)
    {
        try
        {
            if (!Uri.TryCreate(iconUri, UriKind.Absolute, out var uri) || !uri.IsFile) return iconUri;
            var sourcePath = uri.LocalPath;
            var normalizedPath = CachePath($"display-normalized-v1|{sourcePath}|{File.GetLastWriteTimeUtc(sourcePath).Ticks}");
            if (!File.Exists(normalizedPath))
            {
                using var source = new Bitmap(sourcePath);
                using var normalized = NormalizePackageIcon(source);
                normalized.Save(normalizedPath, ImageFormat.Png);
            }
            return new Uri(normalizedPath).AbsoluteUri;
        }
        catch { return iconUri; }
    }

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
            var path = CachePath("normalized-v1|" + identity);
            if (!File.Exists(path))
            {
                using var stream = entry.DisplayInfo.GetLogo(new Windows.Foundation.Size(64, 64)).OpenReadAsync().AsTask().GetAwaiter().GetResult();
                using var input = stream.AsStreamForRead();
                using var source = new Bitmap(input);
                using var normalized = NormalizePackageIcon(source);
                normalized.Save(path, ImageFormat.Png);
            }
            return new Uri(path).AbsoluteUri;
        }
        catch { return null; }
    }

    internal static Bitmap NormalizePackageIcon(Bitmap source)
    {
        var left = source.Width;
        var top = source.Height;
        var right = -1;
        var bottom = -1;
        for (var y = 0; y < source.Height; y++)
            for (var x = 0; x < source.Width; x++)
                if (source.GetPixel(x, y).A > 8)
                {
                    left = Math.Min(left, x); top = Math.Min(top, y);
                    right = Math.Max(right, x); bottom = Math.Max(bottom, y);
                }
        var output = new Bitmap(64, 64, PixelFormat.Format32bppArgb);
        if (right < left || bottom < top) return output;
        // Retain a pixel around the artwork so soft edges aren't clipped.
        left = Math.Max(0, left - 1); top = Math.Max(0, top - 1);
        right = Math.Min(source.Width - 1, right + 1); bottom = Math.Min(source.Height - 1, bottom + 1);
        var bounds = Rectangle.FromLTRB(left, top, right + 1, bottom + 1);
        var scale = 60f / Math.Max(bounds.Width, bounds.Height);
        var width = bounds.Width * scale;
        var height = bounds.Height * scale;
        using var graphics = Graphics.FromImage(output);
        graphics.Clear(Color.Transparent);
        graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
        graphics.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
        using var attributes = new ImageAttributes();
        attributes.SetWrapMode(System.Drawing.Drawing2D.WrapMode.TileFlipXY);
        graphics.DrawImage(source, Rectangle.Round(new RectangleF((64 - width) / 2, (64 - height) / 2, width, height)),
            bounds.X, bounds.Y, bounds.Width, bounds.Height, GraphicsUnit.Pixel, attributes);
        return output;
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
