// SPDX-License-Identifier: GPL-3.0-only
using Microsoft.UI.Reactor;
using MyApps.Browser;

namespace MyApps.Application;

internal static class Program
{
    [STAThread]
    private static void Main() => ReactorApp.Run<WinAppsShell>("WinApps", width: 1100, height: 780);
}
