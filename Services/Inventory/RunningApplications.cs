// SPDX-License-Identifier: GPL-3.0-only
using System.Diagnostics;
using WinApps.Models;
using WinApps.Services.Icons;

namespace WinApps.Services.Inventory;

internal sealed record RunningApplication(int Id, long Started, string Name, string Title, string Path, long Memory, int Threads,
    string? IconUri = null, long CpuTicks = 0, double? CpuPercent = null, nint WindowHandle = 0);

internal static class RunningApplications
{
    internal static RunningApplication[] Read(InstalledApp? app, bool includePresentation = true, CancellationToken cancellationToken = default,
        IReadOnlySet<int>? excludedIds = null)
    {
        if (cancellationToken.IsCancellationRequested) return [];
        var results = new List<RunningApplication>();
        var windows = ApplicationWindows.Read();
        var folder = string.IsNullOrWhiteSpace(app?.InstallPath) ? null : System.IO.Path.GetFullPath(app.InstallPath).TrimEnd('\\') + "\\";
        var processes = Process.GetProcesses();
        try
        {
        foreach (var process in processes)
        {
            using (process)
            {
                if (cancellationToken.IsCancellationRequested) return [];
                try
                {
                    // Existing homepage cards retain their resource snapshot. Only new
                    // windowed processes need paths, icons, and measurements.
                    if (excludedIds?.Contains(process.Id) == true) continue;
                    var handle = process.MainWindowHandle;
                    var title = process.MainWindowTitle;
                    if (app is null)
                    {
                        if (!windows!.TryGetValue(process.Id, out var visible)) continue;
                        if (process.ProcessName.Equals("msedgewebview2", StringComparison.OrdinalIgnoreCase)) continue;
                        handle = visible.Handle;
                        title = visible.Title;
                    }
                    if (app is not null)
                    {
                        if (windows.TryGetValue(process.Id, out var visible)) { handle = visible.Handle; title = visible.Title; }
                        else handle = 0;
                    }
                    if (app is null && (handle == IntPtr.Zero || title.Length == 0)) continue;
                    if (app is null && process.ProcessName.Equals("ApplicationFrameHost", StringComparison.OrdinalIgnoreCase)) continue;
                    if (app is not null && folder is null && app.ExecutablePath is null) continue;
                    string path;
                    try { path = process.MainModule?.FileName ?? ""; } catch { path = ""; }
                    if (app is not null)
                    {
                        if (string.IsNullOrWhiteSpace(app.FamilyName))
                        {
                            // Desktop apps often share System32 or a suite directory.
                            // A folder match must never identify another executable as this app.
                            if (app.ExecutablePath is null || !path.Equals(app.ExecutablePath, StringComparison.OrdinalIgnoreCase)) continue;
                            if (app.ConsolePath is not null && !title.Contains(app.Name, StringComparison.OrdinalIgnoreCase)) continue;
                        }
                        else if (folder is null || !path.StartsWith(folder, StringComparison.OrdinalIgnoreCase)) continue;
                    }
                    var name = process.ProcessName;
                    if (includePresentation && path.Length > 0)
                    {
                        try
                        {
                            var info = FileVersionInfo.GetVersionInfo(path);
                            name = !string.IsNullOrWhiteSpace(info.FileDescription) ? info.FileDescription.Trim()
                                : !string.IsNullOrWhiteSpace(info.ProductName) ? info.ProductName.Trim() : name;
                        }
                        catch { }
                    }
                    results.Add(new(process.Id, process.StartTime.ToUniversalTime().Ticks, app?.Name ?? name,
                        title, path, process.WorkingSet64, process.Threads.Count, includePresentation ? app?.IconUri ?? ApplicationIcons.DesktopIcon(path) : null,
                        process.TotalProcessorTime.Ticks, WindowHandle: handle));
                }
                catch (Exception error) when (error is System.ComponentModel.Win32Exception or InvalidOperationException or NotSupportedException) { }
            }
        }
        }
        finally
        {
            foreach (var process in processes) process.Dispose();
        }
        return results.OrderByDescending(item => item.Memory).ToArray();
    }

    internal static string RequestClose(RunningApplication item)
    {
        try
        {
            using var process = Process.GetProcessById(item.Id);
            if (process.Id == Environment.ProcessId) return "WinApps cannot close itself through this action.";
            if (process.StartTime.ToUniversalTime().Ticks != item.Started ||
                !string.Equals(process.MainModule?.FileName, item.Path, StringComparison.OrdinalIgnoreCase))
                return "The process changed. Refresh and try again.";
            var window = new Windows.Win32.Foundation.HWND(item.WindowHandle);
            if (item.WindowHandle == 0 || !ApplicationWindows.IsAppWindow(window) || !ApplicationWindows.BelongsTo(window, item.Id))
                return "Could not close the application: its window is no longer available. Refresh and try again.";
            if (!Windows.Win32.PInvoke.PostMessage(window, 0x0010, default, default))
                return "Could not request application close. Windows may require matching administrator permissions.";
            return $"Close requested for {item.Name} (PID {item.Id}).";
        }
        catch (Exception error) { return $"Could not close {item.Name}: {error.Message}"; }
    }
}
