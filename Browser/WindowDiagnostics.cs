// SPDX-License-Identifier: GPL-3.0-only
using Microsoft.UI.Xaml;

namespace WinApps.Browser;

internal static class WindowDiagnostics
{
    private static bool _attached;
    private static readonly object Gate = new();

    internal static void Attach()
    {
        if (_attached) return;
        _attached = true;
        Microsoft.UI.Xaml.Application.Current.UnhandledException += (_, args) => Write("WinUI", args.Exception);
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            Write("Runtime", args.ExceptionObject as Exception ?? new Exception(args.ExceptionObject?.ToString()));
    }

    internal static void Write(string operation, Exception error)
    {
        try
        {
            lock (Gate)
            {
                var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WinApps", "errors.log");
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                if (File.Exists(path) && new FileInfo(path).Length > 512 * 1024) File.WriteAllText(path, "");
                File.AppendAllText(path, $"{DateTimeOffset.Now:O} [{operation}] {error}\n\n");
            }
        }
        catch (Exception) { /* Diagnostics must never prevent app recovery or shutdown. */ }
    }
}
