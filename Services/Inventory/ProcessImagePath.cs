// SPDX-License-Identifier: GPL-3.0-only
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace WinApps.Services.Inventory;

internal static partial class ProcessImagePath
{
    internal static unsafe string Read(int processId)
    {
        // Query the executable with limited rights; do not enumerate process modules.
        using var process = OpenProcess(0x1000, false, processId);
        if (process.IsInvalid) return "";
        var buffer = new char[32768];
        var length = (uint)buffer.Length;
        fixed (char* path = buffer)
            return QueryFullProcessImageName(process, 0, path, ref length)
                ? new string(path, 0, (int)length) : "";
    }

    [LibraryImport("kernel32.dll", SetLastError = true)]
    private static partial SafeProcessHandle OpenProcess(uint access,
        [MarshalAs(UnmanagedType.Bool)] bool inheritHandle, int processId);

    [LibraryImport("kernel32.dll", EntryPoint = "QueryFullProcessImageNameW", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static unsafe partial bool QueryFullProcessImageName(SafeProcessHandle process, uint flags, char* path, ref uint length);
}
