// SPDX-License-Identifier: GPL-3.0-only
using Microsoft.UI.Reactor;
using Microsoft.UI.Reactor.Core;
using static Microsoft.UI.Reactor.Factories;

namespace MyApps.Browser;

internal sealed class WinAppsShell : Component
{
    public override Element Render()
    {
        var attempt = UseState(0);
        return ErrorBoundary(Component<PackageExplorer>().WithKey($"explorer-{attempt.Value}"), error =>
            FlexColumn(
                TitleBar("WinApps").Flex(shrink: 0),
                Component<ErrorRecoveryPanel, ErrorRecoveryPanelProps>(new("WinApps couldn't display this view", error,
                    () => attempt.Set(attempt.Value + 1), null)).Flex(grow: 1, basis: 0)))
            .WithKey("app-recovery");
    }
}
