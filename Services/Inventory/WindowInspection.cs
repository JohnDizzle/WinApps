// SPDX-License-Identifier: GPL-3.0-only
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;

namespace WinApps.Services.Inventory;

internal sealed record InspectedWindow(nint Handle, uint ProcessId, uint ThreadId, int Depth, string Title, string Class, bool Visible = false)
{
    public override string ToString()
    {
        var label = string.IsNullOrWhiteSpace(Title) ? $"Untitled {Shorten(Class, 48)}" : Shorten(Title, 90);
        return $"{new string(' ', Depth * 2)}{label} · {(Depth > 0 ? "child" : "window")}{(Visible ? "" : ", hidden")} · 0x{Handle:X}";
    }

    private static string Shorten(string value, int limit) => value.Length <= limit ? value : value[..limit] + "…";
}

internal static unsafe class WindowInspection
{
    internal static uint GetWindowThreadProcessId(nint window, out uint process) => PInvoke.GetWindowThreadProcessId(new HWND(window), out process);
    internal static nint GetAncestor(nint window) => (nint)PInvoke.GetAncestor(new HWND(window), GET_ANCESTOR_FLAGS.GA_ROOT).Value;

    internal static InspectedWindow Read(nint window, int depth = 0)
    {
        var thread = GetWindowThreadProcessId(window, out var process);
        var title = stackalloc char[1024];
        var name = stackalloc char[256];
        var titleLength = PInvoke.GetWindowText(new HWND(window), title, 1024);
        var classLength = PInvoke.GetClassName(new HWND(window), name, 256);
        return new(window, process, thread, depth, new string(title, 0, titleLength), new string(name, 0, classLength), PInvoke.IsWindowVisible(new HWND(window)));
    }

    // Include the UWP frame even when its owning process differs from the packaged app.
    internal static InspectedWindow[] Enumerate(int processId, nint representative)
    {
        var windows = new List<InspectedWindow>();
        PInvoke.EnumWindows((window, _) =>
        {
            PInvoke.GetWindowThreadProcessId(window, out var owner);
            if (owner != processId && (nint)window.Value != representative) return true;
            windows.Add(Read((nint)window.Value));
            if (windows.Count >= 2000) return false;
            PInvoke.EnumChildWindows(window, (child, _) =>
            {
                var depth = 1;
                var parent = PInvoke.GetParent(child);
                while (parent != default && parent != window && depth < 16) { depth++; parent = PInvoke.GetParent(parent); }
                windows.Add(Read((nint)child.Value, depth));
                return windows.Count < 2000;
            }, default);
            return windows.Count < 2000;
        }, default);
        return windows.ToArray();
    }

    internal static string Describe(InspectedWindow target)
    {
        var window = new HWND(target.Handle);
        if (!PInvoke.IsWindow(window) || GetWindowThreadProcessId(target.Handle, out var owner) != target.ThreadId || owner != target.ProcessId)
            return "This window has closed or its handle has been reused. Refresh windows to select another window.";
        var current = Read(target.Handle);
        var hasBounds = PInvoke.GetWindowRect(window, out var bounds);
        var style = (uint)PInvoke.GetWindowLong(window, WINDOW_LONG_PTR_INDEX.GWL_STYLE);
        var extended = (uint)PInvoke.GetWindowLong(window, WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE);
        var states = new List<string>();
        if (PInvoke.IsWindowVisible(window)) states.Add("Visible"); else states.Add("Hidden");
        if (!PInvoke.IsWindowEnabled(window)) states.Add("Disabled");
        if (PInvoke.IsIconic(window)) states.Add("Minimized");
        if (PInvoke.IsZoomed(window)) states.Add("Maximized");
        var styles = new[] { (0x00C00000u, "Caption"), (0x00040000u, "Resizable"), (0x80000000u, "Popup"), (0x40000000u, "Child") };
        return $"Title: {current.Title}\nClass: {current.Class}\nHandle: 0x{target.Handle:X}    PID: {owner}    Thread: {target.ThreadId}\n" +
            $"State: {string.Join(" · ", states)}\n" +
            (hasBounds ? $"Bounds: {bounds.left}, {bounds.top} · {bounds.right - bounds.left} × {bounds.bottom - bounds.top} px\n" : "Bounds: unavailable\n") +
            $"DPI: {PInvoke.GetDpiForWindow(window)}    Parent: 0x{(nint)PInvoke.GetParent(window).Value:X}    Owner: 0x{(nint)PInvoke.GetWindow(window, GET_WINDOW_CMD.GW_OWNER).Value:X}\n" +
            $"Style: 0x{style:X8} ({string.Join(", ", styles.Where(item => (style & item.Item1) == item.Item1).Select(item => item.Item2))})\nExtended style: 0x{extended:X8}";
    }
}
