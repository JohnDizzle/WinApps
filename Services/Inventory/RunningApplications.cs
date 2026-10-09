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
                    var title = process.MainWindowTitle;
                    if (app is null && (process.MainWindowHandle == IntPtr.Zero || title.Length == 0)) continue;
                    if (app is not null && folder is null) continue;
                    string path;
                    try { path = process.MainModule?.FileName ?? ""; } catch { path = ""; }
                    if (app is not null && !path.StartsWith(folder!, StringComparison.OrdinalIgnoreCase)) continue;
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
                        process.TotalProcessorTime.Ticks, WindowHandle: process.MainWindowHandle));
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

    internal static string Terminate(RunningApplication item)
    {
        try
        {
            using var process = Process.GetProcessById(item.Id);
            if (process.Id == Environment.ProcessId) return "WinApps cannot terminate itself.";
            if (process.StartTime.ToUniversalTime().Ticks != item.Started ||
                !string.Equals(process.MainModule?.FileName, item.Path, StringComparison.OrdinalIgnoreCase))
                return "The process changed. Refresh and try again.";
            process.Kill();
            return $"Termination requested for {item.Name} (PID {item.Id}).";
        }
        catch (Exception error) { return $"Could not terminate {item.Name}: {error.Message}"; }
    }
}
