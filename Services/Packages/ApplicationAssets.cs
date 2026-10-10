// SPDX-License-Identifier: GPL-3.0-only
using System.Runtime.InteropServices;
using System.Diagnostics;
using WinApps.Models;

namespace WinApps.Services.Packages;

public static class ApplicationAssets
{
    private static string Destination(InstalledApp app, string extension, string? directory = null)
    {
        directory ??= Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        var name = string.Concat(app.Name.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c)).Trim().TrimEnd('.');
        if (name.Length == 0) name = "Application";
        for (var index = 0; ; index++)
        {
            var path = Path.Combine(directory, name + (index == 0 ? "" : $" ({index})") + extension);
            if (!File.Exists(path) && !Directory.Exists(path)) return path;
        }
    }

    public static string CreateShortcut(InstalledApp app, string? directory = null)
    {
        var destination = Destination(app, ".lnk", directory);
        if (app.LaunchPath is { } existing && File.Exists(existing))
        {
            File.Copy(existing, destination, overwrite: false);
            return destination;
        }
        if (string.IsNullOrWhiteSpace(app.FamilyName) && (app.ExecutablePath is null || !File.Exists(app.ExecutablePath)))
            throw new InvalidOperationException("No executable or shortcut is available for this application.");
        object? shell = null;
        object? shortcut = null;
        try
        {
            shell = Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell")!);
            shortcut = ((dynamic)shell!).CreateShortcut(destination);
            dynamic link = shortcut;
            if (!string.IsNullOrWhiteSpace(app.FamilyName))
            {
                link.TargetPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe");
                link.Arguments = "shell:AppsFolder\\" + app.FullName;
                if (app.IconUri is not null)
                {
                    var dataDirectory = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                    try { dataDirectory = Windows.Storage.ApplicationData.Current.LocalFolder.Path; }
                    catch (Exception error) when (error is InvalidOperationException or COMException) { }
                    var iconDirectory = Path.Combine(dataDirectory, "WinApps", "ShortcutIcons");
                    Directory.CreateDirectory(iconDirectory);
                    link.IconLocation = ExportImage(app, true, iconDirectory) + ",0";
                }
            }
            else
            {
                link.TargetPath = app.ExecutablePath;
                link.Arguments = app.LaunchArguments ?? "";
                link.WorkingDirectory = app.WorkingDirectory ?? "";
                link.IconLocation = app.ExecutablePath;
            }
            link.Description = app.Name;
            link.Save();
            return destination;
        }
        finally
        {
            if (shortcut is not null && Marshal.IsComObject(shortcut)) Marshal.FinalReleaseComObject(shortcut);
            if (shell is not null && Marshal.IsComObject(shell)) Marshal.FinalReleaseComObject(shell);
        }
    }

    public static string ExportImage(InstalledApp app, bool icon, string? directory = null)
    {
        if (!Uri.TryCreate(app.IconUri, UriKind.Absolute, out var source) || !source.IsFile || !File.Exists(source.LocalPath))
            throw new InvalidOperationException("No application artwork is available to export.");
        using var bitmap = new System.Drawing.Bitmap(source.LocalPath);
        using var png = new MemoryStream();
        bitmap.Save(png, System.Drawing.Imaging.ImageFormat.Png);
        var bytes = png.ToArray();
        if (icon && (bitmap.Width > 256 || bitmap.Height > 256)) throw new InvalidOperationException("Icon artwork must be at most 256 pixels.");
        var destination = Destination(app, icon ? ".ico" : ".png", directory);
        using var output = new FileStream(destination, FileMode.CreateNew, FileAccess.Write);
        if (icon)
        {
            using var writer = new BinaryWriter(output, System.Text.Encoding.UTF8, leaveOpen: true);
            writer.Write((ushort)0); writer.Write((ushort)1); writer.Write((ushort)1);
            writer.Write((byte)(bitmap.Width == 256 ? 0 : bitmap.Width));
            writer.Write((byte)(bitmap.Height == 256 ? 0 : bitmap.Height));
            writer.Write((byte)0); writer.Write((byte)0); writer.Write((ushort)1); writer.Write((ushort)32);
            writer.Write(bytes.Length); writer.Write(22);
        }
        output.Write(bytes);
        return destination;
    }

    public static async Task<string> RequestPinAsync(InstalledApp app, bool taskbar)
    {
        if (!string.IsNullOrWhiteSpace(app.FamilyName))
        {
            try
            {
                var package = new Windows.Management.Deployment.PackageManager().FindPackagesForUser("")
                    .FirstOrDefault(item => item.Id.FamilyName == app.FamilyName);
                var entries = package is null ? null : await package.GetAppListEntriesAsync();
                var entry = entries?.FirstOrDefault(item => item.AppUserModelId == app.FullName);
                if (entry is not null)
                {
                    var pinned = taskbar
                        ? await Windows.UI.Shell.TaskbarManager.GetDefault().RequestPinAppListEntryAsync(entry)
                        : await Windows.UI.StartScreen.StartScreenManager.GetDefault().RequestAddAppListEntryAsync(entry);
                    return pinned ? $"{app.Name} pinned to {(taskbar ? "taskbar" : "Start")}." : "Pin request cancelled or unavailable.";
                }
            }
            catch (Exception error) when (error is COMException or UnauthorizedAccessException or NotSupportedException) { }
        }
        if (app.LaunchPath is { } shortcut && File.Exists(shortcut))
            Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{shortcut}\"") { UseShellExecute = true });
        else
            Process.Start(new ProcessStartInfo("explorer.exe", "shell:AppsFolder") { UseShellExecute = true });
        return $"Right-click {app.Name} in Windows and choose Pin to {(taskbar ? "taskbar (under Show more options if needed)" : "Start")}.";
    }
}
