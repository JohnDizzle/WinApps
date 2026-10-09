// SPDX-License-Identifier: GPL-3.0-only
using Microsoft.UI.Reactor;
using WinApps.Browser;
using WinApps.Services.Storage;

namespace WinApps.Application;

internal static class Program
{
    [STAThread]
    private static void Main() => ReactorApp.Run<WinAppsShell>("WinApps",
        width: WindowPlacementStore.DefaultWidth, height: WindowPlacementStore.DefaultHeight);
}
