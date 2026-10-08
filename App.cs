// SPDX-License-Identifier: GPL-3.0-only
using Microsoft.UI.Reactor;
using MyApps.Browser;
using MyApps.Services.Storage;

namespace MyApps.Application;

internal static class Program
{
    [STAThread]
    private static void Main() => ReactorApp.Run<WinAppsShell>("WinApps",
        width: WindowPlacementStore.DefaultWidth, height: WindowPlacementStore.DefaultHeight);
}
