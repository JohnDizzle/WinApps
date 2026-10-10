// SPDX-License-Identifier: GPL-3.0-only
using Microsoft.UI.Reactor;
using Microsoft.UI.Reactor.Core;
using WinApps.Models;
using static Microsoft.UI.Reactor.Factories;
using static WinApps.Browser.BrowserUi;

namespace WinApps.Browser;

internal sealed record AdministrativeToolViewProps(InstalledApp App, Action<string> OnOpenFolder);
internal sealed class AdministrativeToolView : Component<AdministrativeToolViewProps>
{
    public override Element Render()
    {
        var app = Props.App;
        var shortcut = app.LaunchPath ?? (app.FullName.StartsWith("StartMenu:", StringComparison.Ordinal) ? app.FullName[10..] : "");
        return Section("Windows administrative tool",
            Text("This console opens in Microsoft Management Console (MMC). It has no separate MSIX package or package settings."),
            Text("Console location"), ReadOnlyText(app.ConsolePath ?? "", "Console location"),
            Text("Start Menu shortcut"), ReadOnlyText(shortcut, "Start Menu shortcut"),
            ActionButton("Open console folder", () => Props.OnOpenFolder(app.InstallPath)),
            Text("Use Launch tool in the application header. The original shortcut preserves its arguments and Windows elevation behavior."));
    }
}
