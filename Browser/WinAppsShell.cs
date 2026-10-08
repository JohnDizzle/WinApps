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
        var backdrop = UseState(BackdropKind.Mica);
        var navigationVisible = UseState(true);
        var window = UseWindow();
        _toggleNavigation = () => navigationVisible.Set(!navigationVisible.Value);
        var darkTheme = theme.Value == ElementTheme.Dark || (theme.Value == ElementTheme.Default &&
            Microsoft.UI.Xaml.Application.Current.RequestedTheme == ApplicationTheme.Dark);
        var themeLabel = darkTheme ? "Switch to light theme" : "Switch to dark theme";
        var appearanceSettings = Button(FluentGlyph("\uE713", 20), () => { })
            .Width(40).Height(40).Padding(0).CornerRadius(10).AutomationName("Appearance settings")
            .Background(Theme.LayerFill).WithBorder(Theme.CardStroke)
            .Set(control =>
            {
                Microsoft.UI.Xaml.Controls.ToolTipService.SetToolTip(control, "Theme and backdrop");
                var menu = new Microsoft.UI.Xaml.Controls.MenuFlyout
                {
                    Placement = Microsoft.UI.Xaml.Controls.Primitives.FlyoutPlacementMode.BottomEdgeAlignedRight
                };
                var themeItem = new Microsoft.UI.Xaml.Controls.MenuFlyoutItem
                {
                    Text = themeLabel,
                    RequestedTheme = theme.Value,
                    Icon = new Microsoft.UI.Xaml.Controls.FontIcon
                    {
                        Glyph = darkTheme ? "\uE706" : "\uE708",
                        FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Segoe Fluent Icons")
                    }
                };
                themeItem.Click += (_, _) => theme.Set(darkTheme ? ElementTheme.Light : ElementTheme.Dark);
                menu.Items.Add(themeItem);
                var backdropMenu = new Microsoft.UI.Xaml.Controls.MenuFlyoutSubItem
                {
                    Text = "Backdrop",
                    RequestedTheme = theme.Value
                };
                foreach (var (label, kind) in new[] {
                    ("Mica", BackdropKind.Mica),
                    ("Acrylic", BackdropKind.DesktopAcrylic),
                    ("Mica Alt", BackdropKind.MicaAlt) })
                {
                    var item = new Microsoft.UI.Xaml.Controls.ToggleMenuFlyoutItem
                    {
                        Text = label,
                        IsChecked = backdrop.Value == kind,
                        RequestedTheme = theme.Value
                    };
                    item.Click += (_, _) => backdrop.Set(kind);
                    backdropMenu.Items.Add(item);
                }
                menu.Items.Add(backdropMenu);
                control.Flyout = menu;
            });

        // Window material and theme must belong to the host root, outside recovery boundaries.
        return WindowFrame(theme.Value,
            TitleBar("WinApps").Height(48)
                .PaneToggleButtonVisible(true)
                .Set(control =>
                {
                    if (window?.NativeWindow is { } nativeWindow)
                    {
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
                    if (control.Tag as string == "navigation-toggle") return;
                    control.Tag = "navigation-toggle";
                    control.PaneToggleRequested += (_, _) => _toggleNavigation?.Invoke();
                })
                .RightHeader(appearanceSettings.HAlign(HorizontalAlignment.Right))
                .AutoRefreshDragRegions(true).Flex(shrink: 0),
            ErrorBoundary(Component<PackageExplorer, PackageExplorerProps>(new(navigationVisible.Value)).WithKey($"explorer-{attempt.Value}"), error =>
                Component<ErrorRecoveryPanel, ErrorRecoveryPanelProps>(new("WinApps couldn't display this view", error,
                    () => attempt.Set(attempt.Value + 1), null)))
                .WithKey("app-recovery").Flex(grow: 1, basis: 0)
        , backdrop.Value);
    }
    internal static Element WindowFrame(ElementTheme theme, Element titleBar, Element content, BackdropKind backdrop = BackdropKind.Mica) =>
        FlexColumn(titleBar, content).RequestedTheme(theme).Backdrop(backdrop);
}
