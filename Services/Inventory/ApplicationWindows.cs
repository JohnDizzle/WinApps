// SPDX-License-Identifier: GPL-3.0-only
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;

namespace WinApps.Services.Inventory;

internal static class ApplicationWindows
{
    internal static unsafe Dictionary<int, (nint Handle, string Title)> Read()
    {
        var windows = new Dictionary<int, (nint, string)>();
        PInvoke.EnumWindows((window, _) =>
        {
            if (!IsAppWindow(window)) return true;
            var buffer = stackalloc char[1024];
            var length = PInvoke.GetWindowText(window, buffer, 1024);
            if (length == 0) return true;
            var title = new string(buffer, 0, length);
            PInvoke.GetWindowThreadProcessId(window, out var owner);
            windows.TryAdd((int)owner, ((nint)window.Value, title));
            // Only Windows' UWP frame delegates its app identity to a child.
            // Other cross-process children are embedded controls (for example WebView2).
            var classLength = PInvoke.GetClassName(window, buffer, 1024);
            if (new string(buffer, 0, classLength) != "ApplicationFrameWindow") return true;
            PInvoke.EnumChildWindows(window, (child, _) =>
            {
                PInvoke.GetWindowThreadProcessId(child, out var childOwner);
                if (childOwner != owner && PInvoke.IsWindowVisible(child))
                    windows.TryAdd((int)childOwner, ((nint)window.Value, title));
                return true;
            }, default);
            return true;
        }, default);
        return windows;
    }

    internal static bool BelongsTo(HWND window, int processId)
    {
        PInvoke.GetWindowThreadProcessId(window, out var owner);
        if (owner == processId) return true;
        if (!IsApplicationFrame(window)) return false;
        var found = false;
        PInvoke.EnumChildWindows(window, (child, _) =>
        {
            PInvoke.GetWindowThreadProcessId(child, out var childOwner);
            if (childOwner == processId) found = true;
            return !found;
        }, default);
        return found;
    }

    internal static unsafe bool IsAppWindow(HWND window)
    {
        if (!PInvoke.IsWindowVisible(window) || window == PInvoke.GetShellWindow() ||
            PInvoke.GetWindow(window, GET_WINDOW_CMD.GW_OWNER) != default) return false;
        var classBuffer = stackalloc char[256];
        var classLength = PInvoke.GetClassName(window, classBuffer, 256);
        if (new string(classBuffer, 0, classLength) is "Shell_TrayWnd" or "Shell_SecondaryTrayWnd" or "Progman" or "WorkerW") return false;
        var style = (uint)PInvoke.GetWindowLong(window, WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE);
        // Tool windows and activation-free overlays are not application windows.
        if ((style & (0x00000080u | 0x08000000u)) != 0) return false;
        uint cloaked = 0;
        PInvoke.DwmGetWindowAttribute(window, Windows.Win32.Graphics.Dwm.DWMWINDOWATTRIBUTE.DWMWA_CLOAKED,
            &cloaked, sizeof(uint));
        return cloaked == 0;
    }

    private static unsafe bool IsApplicationFrame(HWND window)
    {
        var buffer = stackalloc char[256];
        var length = PInvoke.GetClassName(window, buffer, 256);
        return new string(buffer, 0, length) == "ApplicationFrameWindow";
    }
}
