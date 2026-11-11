// SPDX-License-Identifier: GPL-3.0-only
using Microsoft.UI.Xaml;
using Microsoft.UI.Reactor;
using Microsoft.UI.Reactor.Core;
using static Microsoft.UI.Reactor.Factories;
using static WinApps.Browser.BrowserUi;

namespace WinApps.Browser;

internal sealed class WinAppsShell : Component
{
    public override Element Render()
    {
        var attempt = UseState(0);
        var theme = UseState(ElementTheme.Default);
        var backdrop = UseState(BackdropKind.MicaAlt);
        var navigationVisible = UseState(true);
        var window = UseWindow();
        var darkTheme = theme.Value == ElementTheme.Dark || (theme.Value == ElementTheme.Default &&
            Microsoft.UI.Xaml.Application.Current.RequestedTheme == ApplicationTheme.Dark);
        var appearanceSettings = GlassButton(Button(FluentGlyph("\uE713", 18), () => { }))
            .Width(44).Height(40).Padding(8).CornerRadius(10).AutomationName("Settings")
            .ToolTip("Settings and about WinApps")
            .Set(control =>
            {
                control.Flyout = SettingsFlyout.Create(theme.Value, darkTheme, backdrop.Value,
                    () => theme.Set(darkTheme ? ElementTheme.Light : ElementTheme.Dark),
                    kind => backdrop.Set(kind));
            });

        var compactButton = MiniModeButton(() =>
            {
                if (window?.NativeWindow is { } nativeWindow) CompactSearchWindowController.EnterCompactMode(nativeWindow);
            });

        // Window material and theme must belong to the host root, outside recovery boundaries.
        return WindowFrame(theme.Value,
            TitleBar("WinApps").Height(48)
                .PaneToggleButtonVisible(true)
                .PaneToggleRequested(() => navigationVisible.Set(!navigationVisible.Value))
                .Set(control =>
                {
                    if (window?.NativeWindow is { } nativeWindow)
                    {
                        WindowPlacementController.Attach(nativeWindow);
                        CompactSearchWindowController.ConfigureAppearance(nativeWindow, theme.Value, backdrop.Value);
                        nativeWindow.ExtendsContentIntoTitleBar = true;
                        window.AppWindow.TitleBar.PreferredHeightOption = Microsoft.UI.Windowing.TitleBarHeightOption.Tall;
                        window.AppWindow.TitleBar.ButtonForegroundColor = darkTheme ? Microsoft.UI.Colors.White : Microsoft.UI.Colors.Black;
                        window.AppWindow.TitleBar.ButtonBackgroundColor = Microsoft.UI.Colors.Transparent;
                    }
                    control.Resources["TitleBarMinDragRegionWidth"] = 8d;
                    control.LeftHeader ??= new Microsoft.UI.Xaml.Controls.Border
                    {
                        Width = 40,
                        Child = new Microsoft.UI.Xaml.Controls.Image
                        {
                            Width = 28, Height = 28,
                            Source = new Microsoft.UI.Xaml.Media.Imaging.BitmapImage(new Uri(
                                Path.Combine(AppContext.BaseDirectory, "Assets", "Square44x44Logo.png")))
                        }
                    };
                })
                .RightHeader(HStack(10, compactButton, appearanceSettings)
                    .Margin(left: 8, top: 4, right: 8, bottom: 4).HAlign(HorizontalAlignment.Right))
                .AutoRefreshDragRegions(true).Flex(shrink: 0),
            FlexColumn(
            Component<SuggestionPolicyNotice>().WithKey("suggestion-policy-notice").Flex(shrink: 0),
            ErrorBoundary(Component<PackageExplorer, PackageExplorerProps>(new(navigationVisible.Value, compact => navigationVisible.Set(!compact))).WithKey($"explorer-{attempt.Value}"), error =>
                Component<ErrorRecoveryPanel, ErrorRecoveryPanelProps>(new("WinApps couldn't display this view", error,
                    () => attempt.Set(attempt.Value + 1), null)))
                .WithKey("app-recovery").Flex(grow: 1, basis: 0)).MinHeight(0).Flex(grow: 1, basis: 0)
        , backdrop.Value);
    }
    internal static ButtonElement MiniModeButton(Action open) => GlassButton(Button(FluentGlyph("\uE73F", 18), open))
        .Width(44).Height(40).Padding(8).CornerRadius(10).AutomationName("Open Mini Mode")
        .ToolTip("Open Mini Mode");

    internal static Element WindowFrame(ElementTheme theme, Element titleBar, Element content, BackdropKind backdrop = BackdropKind.MicaAlt) =>
        FlexColumn(
            Border(titleBar).WithBorder(Theme.Ref("DividerStrokeColorDefaultBrush"))
                .BorderThickness(0, 0, 0, 1).Flex(shrink: 0),
            content).RequestedTheme(theme).Backdrop(backdrop)
            .Set(control =>
            {
                AppearanceResources.Apply(control);
                BrowserMotion.AppearanceTransition(control, $"{theme}-{backdrop}");
            });
}
