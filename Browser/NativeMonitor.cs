// SPDX-License-Identifier: GPL-3.0-only
using System.Runtime.InteropServices;
using Windows.Graphics;

namespace MyApps.Browser;

internal static class NativeMonitor
{
    [StructLayout(LayoutKind.Sequential)]
    private struct Point { public int X, Y; }
    [StructLayout(LayoutKind.Sequential)]
    private struct Rect { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)]
    private struct MonitorInfo
    {
        public uint Size;
        public Rect Monitor, Work;
        public uint Flags;
    }

    [DllImport("user32.dll")]
    private static extern nint MonitorFromPoint(Point point, uint flags);
    [DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(nint window);

    internal static double WindowScale(Microsoft.UI.Xaml.Window window)
    {
        var dpi = GetDpiForWindow(WinRT.Interop.WindowNative.GetWindowHandle(window));
        return dpi == 0 ? 1 : dpi / 96d;
    }
    [DllImport("user32.dll", EntryPoint = "GetMonitorInfoW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetMonitorInfo(nint monitor, ref MonitorInfo info);
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SystemParametersInfoW(uint action, uint parameter, out Rect value, uint flags);

    internal static RectInt32 WorkArea(int x, int y)
    {
        // Native monitor lookup avoids WinRT's failing display-collection iterator.
        var handle = MonitorFromPoint(new Point { X = Math.Max(0, x), Y = Math.Max(0, y) }, 2);
        var info = new MonitorInfo { Size = (uint)Marshal.SizeOf<MonitorInfo>() };
        if (GetMonitorInfo(handle, ref info) && info.Work.Left >= 0 && info.Work.Top >= 0)
            return Bounds(info.Work);
        handle = MonitorFromPoint(new Point(), 1);
        if (GetMonitorInfo(handle, ref info)) return Bounds(info.Work);
        if (SystemParametersInfoW(0x0030, 0, out var primaryWork, 0)) return Bounds(primaryWork);
        return new RectInt32(0, 0, 1200, 960);
    }

    private static RectInt32 Bounds(Rect rect) => new(Math.Max(0, rect.Left), Math.Max(0, rect.Top),
        Math.Max(1, rect.Right - Math.Max(0, rect.Left)), Math.Max(1, rect.Bottom - Math.Max(0, rect.Top)));
}
