// SPDX-License-Identifier: GPL-3.0-only
using System.Diagnostics;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;

namespace WinApps.Services.Inventory;

internal static class ApplicationWindowSwitcher
{
    internal static bool HasOpenWindow(RunningApplication app)
    {
        if (app.WindowHandle == 0) return false;
        var window = new HWND(app.WindowHandle);
        if (!PInvoke.IsWindow(window) || !PInvoke.IsWindowVisible(window)) return false;
        PInvoke.GetWindowThreadProcessId(window, out var owner);
        return owner == app.Id;
    }
    internal static string SwitchTo(RunningApplication app)
    {
        try
        {
            using var process = Process.GetProcessById(app.Id);
            if (process.StartTime.ToUniversalTime().Ticks != app.Started)
                return "This application has restarted. Wait for the cards to refresh.";
            var handle = process.MainWindowHandle;
            if (handle == 0) return "This application no longer has an accessible window.";
            var window = new HWND(handle);
            PInvoke.GetWindowThreadProcessId(window, out var owner);
            if (owner != app.Id) return "The application window changed. Wait for the cards to refresh.";
            if (PInvoke.IsIconic(window)) PInvoke.ShowWindowAsync(window, SHOW_WINDOW_CMD.SW_RESTORE);
            return PInvoke.SetForegroundWindow(window) ? "" : "Windows couldn't bring this app to the foreground. Try its taskbar button.";
        }
        catch (Exception error) { return $"Couldn't switch to {app.Name}: {error.Message}"; }
    }
}
