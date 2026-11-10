// SPDX-License-Identifier: GPL-3.0-only
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Foundation;

namespace WinApps.Services.Inventory;

// Keep managed state rooted explicitly; no runtime-generated delegate/COM stubs.
internal static unsafe class NativeWindowEnumeration
{
    private sealed class State(Func<HWND, bool> visit) { internal Func<HWND, bool> Visit = visit; internal Exception? Error; }
    internal static void All(Func<HWND, bool> visit) => Run(default, visit, false);
    internal static void Children(HWND parent, Func<HWND, bool> visit) => Run(parent, visit, true);
    private static void Run(HWND parent, Func<HWND, bool> visit, bool children)
    {
        var state = new State(visit);
        var handle = GCHandle.Alloc(state);
        try
        {
            var parameter = new LPARAM(GCHandle.ToIntPtr(handle));
            if (children) PInvoke.EnumChildWindows(parent, &Visit, parameter);
            else PInvoke.EnumWindows(&Visit, parameter);
            if (state.Error is not null) ExceptionDispatchInfo.Capture(state.Error).Throw();
        }
        finally { handle.Free(); }
    }
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static BOOL Visit(HWND window, LPARAM parameter)
    {
        var state = (State)GCHandle.FromIntPtr(parameter.Value).Target!;
        try { return state.Visit(window); }
        catch (Exception error) { state.Error = error; return false; }
    }
}
