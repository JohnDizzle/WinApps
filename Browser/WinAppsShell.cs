// SPDX-License-Identifier: GPL-3.0-only
using Microsoft.UI.Xaml;
using Microsoft.UI.Reactor;
using Microsoft.UI.Reactor.Core;
using static Microsoft.UI.Reactor.Factories;
using static MyApps.Browser.BrowserUi;

namespace MyApps.Browser;

internal sealed class WinAppsShell : Component
{
    private Action? _toggleNavigation;
    public override Element Render()
    {
        var attempt = UseState(0);
        var theme = UseState(ElementTheme.Default);
        var navigationVisible = UseState(true);
        _toggleNavigation = () => navigationVisible.Set(!navigationVisible.Value);
        var darkTheme = theme.Value == ElementTheme.Dark || (theme.Value == ElementTheme.Default &&
            Microsoft.UI.Xaml.Application.Current.RequestedTheme == ApplicationTheme.Dark);
        var themeLabel = darkTheme ? "Switch to light theme" : "Switch to dark theme";
        var themeToggle = Button(FluentGlyph(darkTheme ? "\uE706" : "\uE708", 20),
            () => theme.Set(darkTheme ? ElementTheme.Light : ElementTheme.Dark)).AutomationName(themeLabel)
            .Width(36).Height(36).Padding(0).CornerRadius(10)
            .Background(Theme.LayerFill).WithBorder(Theme.CardStroke)
            .Set(control => Microsoft.UI.Xaml.Controls.ToolTipService.SetToolTip(control, themeLabel));

        // Window material and theme must belong to the host root, outside recovery boundaries.
        return WindowFrame(theme.Value,
            TitleBar("WinApps").Icon(FontIcon("\uE71D", "Segoe Fluent Icons"))
                .PaneToggleButtonVisible(true)
                .Set(control =>
                {
                    if (control.Tag as string == "navigation-toggle") return;
                    control.Tag = "navigation-toggle";
                    control.PaneToggleRequested += (_, _) => _toggleNavigation?.Invoke();
                })
                .RightHeader(themeToggle).AutoRefreshDragRegions(true).Flex(shrink: 0),
            ErrorBoundary(Component<PackageExplorer, PackageExplorerProps>(new(navigationVisible.Value)).WithKey($"explorer-{attempt.Value}"), error =>
                Component<ErrorRecoveryPanel, ErrorRecoveryPanelProps>(new("WinApps couldn't display this view", error,
                    () => attempt.Set(attempt.Value + 1), null)))
                .WithKey("app-recovery").Flex(grow: 1, basis: 0)
        );
    }
    internal static Element WindowFrame(ElementTheme theme, Element titleBar, Element content) =>
        FlexColumn(titleBar, content).RequestedTheme(theme).Backdrop(BackdropKind.Mica);
}
