// SPDX-License-Identifier: GPL-3.0-only
using Microsoft.UI.Reactor;
using WinApps.Browser;
using WinApps.Services.Storage;

namespace WinApps.Application;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        var overviewRequested = SuggestionJumpList.RequestedApp is not null;
        if (!CompactWindowStore.Shared.Load().StartInCompactMode && !overviewRequested)
        {
            ReactorApp.Run<WinAppsShell>("WinApps",
                width: WindowPlacementStore.DefaultWidth, height: WindowPlacementStore.DefaultHeight);
            return;
        }

        ReactorApp.Run(_ =>
        {
            var main = ReactorApp.OpenWindow(new WindowSpec
            {
                Title = "WinApps", Width = WindowPlacementStore.DefaultWidth,
                Height = WindowPlacementStore.DefaultHeight, ActivateOnOpen = false
            }, () => new WinAppsShell(), host => WindowPlacementController.Attach(host.Window, deferMaximize: true));
            if (!overviewRequested) CompactSearchWindowController.EnterCompactMode(main.NativeWindow);
        });
    }
}
