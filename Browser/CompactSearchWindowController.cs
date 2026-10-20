// SPDX-License-Identifier: GPL-3.0-only
using Microsoft.UI.Windowing;
using Microsoft.UI.Reactor.Core;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Markup;
using Microsoft.UI.Xaml.Media.Imaging;
using WinApps.Models;
using WinApps.Services.Storage;
using Windows.Graphics;
using Windows.System;

namespace WinApps.Browser;

// The main window owns this companion, including its lifetime and current library.
internal sealed class CompactSearchWindowController
{
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Window, CompactSearchWindowController> Controllers = new();
    private readonly Window _main;
    private readonly AppWindow _mainAppWindow;
    private AppWindow? _companionAppWindow;
    private readonly CompactWindowStore _placementStore = CompactWindowStore.Shared;
    private readonly DispatcherTimer _placementTimer = new() { Interval = TimeSpan.FromMilliseconds(350) };
    private Window? _searchWindow;
    private Grid? _root;
    private TextBox? _search;
    private TextBlock? _status;
    private Grid? _badges;
    private readonly List<CompactActivityTile> _activityTiles = [];
    private CompactAppActivity? _activity;
    private HashSet<string> _activeKeys = new(StringComparer.Ordinal);
    private int _badgeColumns = 3;
    private InstalledApp[] _apps = [];
    private InstalledApp[] _mostViewed = [];
    private InstalledApp[] _matches = [];
    private Action<InstalledApp>? _select;
    private ApplicationViewHistory? _history;
    private string? _exposureSignature;
    private bool _learningEnabled;
    private bool _compactMode;
    private ElementTheme _theme;
    private BackdropKind _backdrop = BackdropKind.MicaAlt;
    private BackdropKind? _appliedBackdrop;
    private bool _wasMaximized;
    private Windows.Graphics.PointInt32 _mainPosition;
    private bool _closed;
    private bool _quitDialogOpen;
    private string? _pendingOverview;

    internal static void ConfigureAppearance(Window main, ElementTheme theme, BackdropKind backdrop)
    {
        var controller = Controllers.GetValue(main, window => new CompactSearchWindowController(window));
        controller._theme = theme;
        controller._backdrop = backdrop;
        controller.ApplyAppearance();
    }

    internal static void EnterCompactMode(Window main)
    {
        var controller = Controllers.GetValue(main, window => new CompactSearchWindowController(window));
        controller._mainPosition = main.AppWindow.Position;
        controller._wasMaximized = main.AppWindow.Presenter is OverlappedPresenter { State: OverlappedPresenterState.Maximized };
        controller._compactMode = true;
        // Keep the main window available if creating the companion fails.
        controller.Show();
        controller._searchWindow!.AppWindow.IsShownInSwitchers = true;
        main.AppWindow.Hide();
    }

    internal static void Update(Window main, InstalledApp[] apps, InstalledApp[] mostViewed, Action<InstalledApp>? select, ApplicationViewHistory? history = null,
        string? selectedKey = null, bool overviewLoading = false)
    {
        var controller = Controllers.GetValue(main, window => new CompactSearchWindowController(window));
        // Activity summaries rerender the explorer frequently; retain keyboard focus
        // in existing badges unless the library, ranking, or availability changed.
        var learningChanged = controller._learningEnabled != (history?.Policy.Enabled ?? false);
        var changed = learningChanged || !ReferenceEquals(controller._apps, apps)
            || !controller._mostViewed.Select(app => app.FullName).SequenceEqual(mostViewed.Select(app => app.FullName))
            || (controller._select is null) != (select is null);
        controller._apps = apps;
        controller._mostViewed = mostViewed;
        controller._select = select;
        controller._history = history;
        controller._learningEnabled = history?.Policy.Enabled ?? false;
        if (learningChanged) controller._exposureSignature = null;
        if (changed && controller._pendingOverview is null) controller.RefreshSuggestions();
        if (controller._pendingOverview is { } pending && selectedKey == pending && !overviewLoading)
            controller.RestoreMain();
    }

    private CompactSearchWindowController(Window main)
    {
        _main = main;
        _mainAppWindow = main.AppWindow;
        _placementTimer.Tick += PlacementTick;
        _mainPosition = main.AppWindow.Position;
        _wasMaximized = main.AppWindow.Presenter is OverlappedPresenter { State: OverlappedPresenterState.Maximized };
        main.AppWindow.Changed += MainChanged;
        main.Closed += MainClosed;
    }

    private void MainChanged(AppWindow sender, AppWindowChangedEventArgs args)
    {
        if (_closed) return;
        var minimized = sender.Presenter is OverlappedPresenter { State: OverlappedPresenterState.Minimized };
        if (!minimized)
        {
            _mainPosition = sender.Position;
            _wasMaximized = sender.Presenter is OverlappedPresenter { State: OverlappedPresenterState.Maximized };
        }
    }

    private void Show()
    {
        EnsureWindow();
        if (_root is null || _searchWindow is null || _search is null) return;
        ApplyAppearance();
        _exposureSignature = null;
        _search.Text = "";
        RefreshSuggestions();
        var settings = _placementStore.Load();
        var previous = settings.RememberPrevious ? settings.Placement : null;
        var position = previous is not null ? new PointInt32(previous.X, previous.Y) : _mainPosition;
        var work = NativeMonitor.WorkArea(position.X, position.Y);
        var scale = NativeMonitor.WindowScale(_main);
        var width = Math.Min(work.Width, (int)Math.Round(CompactWindowStore.DefaultWidth * scale));
        var height = Math.Min(work.Height, (int)Math.Round(CompactWindowStore.DefaultHeight * scale));
        var placement = CompactWindowStore.Fit(previous ?? new WindowPlacement(work.X + (work.Width - width) / 2,
            work.Y + Math.Max(0, work.Height - height - (int)Math.Round(24 * scale)), width, height),
            work.X, work.Y, work.Width, work.Height);
        _searchWindow.AppWindow.MoveAndResize(new RectInt32(placement.X, placement.Y, placement.Width, placement.Height));
        _searchWindow.Activate();
        RefreshSuggestions();
        _activity?.Refresh();
        _search.DispatcherQueue.TryEnqueue(() => _search.Focus(FocusState.Programmatic));
    }

    private void EnsureWindow()
    {
        if (_searchWindow is not null) return;
        WindowDiagnostics.Attach();
        _root = (Grid)XamlReader.Load("""
            <Grid xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                  xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                  Background="{ThemeResource LayerFillColorDefaultBrush}" Padding="12" RowSpacing="8">
                <Grid.Resources>
                    <Style x:Key="LoadingLine" TargetType="Border">
                        <Setter Property="Background" Value="{ThemeResource TextFillColorSecondaryBrush}" />
                        <Setter Property="CornerRadius" Value="4" />
                    </Style>
                    <Style x:Key="InstanceBadge" TargetType="Border">
                        <Setter Property="Background" Value="{ThemeResource SolidBackgroundFillColorSecondaryBrush}" />
                        <Setter Property="BorderBrush" Value="{ThemeResource TextFillColorSecondaryBrush}" />
                        <Setter Property="BorderThickness" Value="1" />
                        <Setter Property="CornerRadius" Value="8" />
                        <Setter Property="Padding" Value="5,1" />
                    </Style>
                    <Style x:Key="SuggestionBadge" TargetType="Button">
                        <Setter Property="Padding" Value="10" />
                        <Setter Property="Margin" Value="4" />
                        <Setter Property="HorizontalContentAlignment" Value="Stretch" />
                        <Setter Property="VerticalContentAlignment" Value="Stretch" />
                        <Setter Property="Background" Value="{ThemeResource ControlFillColorDefaultBrush}" />
                        <Setter Property="BorderBrush" Value="{ThemeResource CardStrokeColorDefaultBrush}" />
                        <Setter Property="CornerRadius" Value="10" />
                    </Style>
                </Grid.Resources>
                <Grid.RowDefinitions>
                    <RowDefinition Height="Auto" />
                    <RowDefinition Height="Auto" />
                    <RowDefinition Height="Auto" />
                    <RowDefinition Height="*" />
                    <RowDefinition Height="Auto" />
                </Grid.RowDefinitions>
                <TitleBar x:Name="CompactTitleBar" Title="Mini WinApps" Height="48" Margin="-12,-12,-12,0" />
                <TextBox Grid.Row="1" x:Name="Search" PlaceholderText="Search your library" MinHeight="40" />
                <TextBlock x:Name="Status" Grid.Row="2" TextWrapping="Wrap" FontSize="12"
                           Foreground="{ThemeResource TextFillColorSecondaryBrush}" />
                <Border Grid.Row="3" CornerRadius="12" Padding="4"
                        Background="{ThemeResource CardBackgroundFillColorDefaultBrush}"
                        BorderBrush="{ThemeResource CardStrokeColorDefaultBrush}" BorderThickness="1">
                    <ScrollViewer HorizontalScrollBarVisibility="Disabled" HorizontalScrollMode="Disabled"
                                  VerticalScrollBarVisibility="Visible" Padding="4,4,12,16">
                        <Grid x:Name="Badges" ColumnSpacing="8" RowSpacing="8" HorizontalAlignment="Stretch" />
                    </ScrollViewer>
                </Border>
                <Button x:Name="OpenFull" Grid.Row="4" Content="Back to main" HorizontalAlignment="Right" />
            </Grid>
            """);
        AppearanceResources.Apply(_root);
        _search = (TextBox)_root.FindName("Search");
        _status = (TextBlock)_root.FindName("Status");
        _badges = (Grid)_root.FindName("Badges");
        _badges.SizeChanged += BadgesSizeChanged;
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(_search, "Search applications");
        _search.TextChanged += SearchTextChanged;
        _root.KeyDown += (_, args) =>
        {
            if (_closed) return;
            if (args.Key == VirtualKey.Escape)
            {
                RestoreMain();
                args.Handled = true;
            }
        };
        _search.KeyDown += (_, args) =>
        {
            if (_closed) return;
            if (args.Key == VirtualKey.Enter && _matches.Length > 0 && _select is not null)
            {
                Open(_matches[0]);
                args.Handled = true;
            }
            else if (args.Key == VirtualKey.Down && _badges.Children.FirstOrDefault() is Button first)
            {
                first.Focus(FocusState.Keyboard);
                args.Handled = true;
            }
        };
        var backButton = (Button)_root.FindName("OpenFull");
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(backButton, "Back to main");
        backButton.Click += (_, _) => RestoreMain();
        _searchWindow = new Window
        {
            Title = "Mini WinApps", Content = _root
        };
        _companionAppWindow = _searchWindow.AppWindow;
        _searchWindow.ExtendsContentIntoTitleBar = true;
        var titleBar = (TitleBar)_root.FindName("CompactTitleBar");
        titleBar.Resources["TitleBarMinDragRegionWidth"] = 8d;
        titleBar.LeftHeader = new Border
        {
            Width = 40,
            Child = new Image
            {
                Width = 28, Height = 28,
                Source = new BitmapImage(new Uri(Path.Combine(AppContext.BaseDirectory, "Assets", "Square44x44Logo.png")))
            }
        };
        _searchWindow.SetTitleBar(titleBar);
        _searchWindow.AppWindow.TitleBar.PreferredHeightOption = TitleBarHeightOption.Tall;
        _root.ActualThemeChanged += CompactThemeChanged;
        ApplyAppearance();
        _searchWindow.AppWindow.IsShownInSwitchers = false;
        _searchWindow.AppWindow.SetIcon(Path.Combine(AppContext.BaseDirectory, "Assets", "WinApps.ico"));
        if (_searchWindow.AppWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.IsMaximizable = false;
            presenter.IsMinimizable = false;
            presenter.PreferredMinimumWidth = 360;
            presenter.PreferredMinimumHeight = 240;
        }
        _searchWindow.AppWindow.Changed += CompactChanged;
        _searchWindow.AppWindow.Closing += CompactClosing;
        _activity = new CompactAppActivity(_searchWindow, () => _apps, () => _activityTiles, keys =>
        {
            if (_closed || _activeKeys.SetEquals(keys)) return;
            _activeKeys = keys.ToHashSet(StringComparer.Ordinal);
            if (_pendingOverview is null && string.IsNullOrWhiteSpace(_search?.Text)) RefreshSuggestions();
        });
        _activity.Start();
    }

    private void PlacementTick(object? sender, object args)
    {
        if (_closed) return;
        try { SaveCompactPlacement(); }
        catch (Exception error) { WindowDiagnostics.Write("Compact placement", error); }
    }

    private void BadgesSizeChanged(object sender, SizeChangedEventArgs args)
    {
        if (_closed) return;
        var columns = Math.Max(1, (int)Math.Floor((args.NewSize.Width + 8) / 164));
        if (columns == _badgeColumns) return;
        _badgeColumns = columns;
        LayoutBadges();
    }

    private void SearchTextChanged(object sender, TextChangedEventArgs args)
    {
        if (!_closed) RefreshSuggestions();
    }

    private void CompactThemeChanged(FrameworkElement sender, object args)
    {
        if (!_closed) ApplyCaptionTheme();
    }

    private void CompactClosing(AppWindow sender, AppWindowClosingEventArgs args)
    {
        if (_closed) return;
        args.Cancel = true;
        ConfirmQuit();
    }

    private async void ConfirmQuit()
    {
        if (_closed || _quitDialogOpen || _root?.XamlRoot is null) return;
        _quitDialogOpen = true;
        try
        {
            var dialog = new ContentDialog
            {
                XamlRoot = _root.XamlRoot,
                RequestedTheme = _root.ActualTheme,
                Title = "Quit WinApps?",
                Content = new TextBlock
                {
                    Text = "This closes Mini WinApps and the main WinApps window. Other applications stay open.",
                    TextWrapping = TextWrapping.Wrap
                },
                PrimaryButtonText = "Quit WinApps",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Close
            };
            // This companion owns a native WinUI tree, not a Reactor dialog host.
#pragma warning disable REACTOR_DIALOG_001
            var result = await dialog.ShowAsync();
#pragma warning restore REACTOR_DIALOG_001
            if (_closed) return;
            if (result == ContentDialogResult.Primary)
            {
                SaveCompactPlacement();
                _main.Close();
            }
        }
        catch (Exception error) { Report($"Could not close WinApps: {error.Message}"); }
        finally { _quitDialogOpen = false; }
    }

    private void CompactChanged(AppWindow app, AppWindowChangedEventArgs args)
    {
        if (!_compactMode || !app.IsVisible || !(args.DidPositionChange || args.DidSizeChange)) return;
        _placementTimer.Stop();
        _placementTimer.Start();
    }

    private void SaveCompactPlacement()
    {
        _placementTimer.Stop();
        if (_closed) return;
        if (!_compactMode || _searchWindow?.AppWindow is not { IsVisible: true } app
            || app.Presenter is not OverlappedPresenter { State: OverlappedPresenterState.Restored }) return;
        _placementStore.SavePlacement(new WindowPlacement(app.Position.X, app.Position.Y, app.Size.Width, app.Size.Height));
    }

    private void ApplyAppearance()
    {
        if (_root is null || _searchWindow is null) return;
        _root.RequestedTheme = _theme;
        // Each native window owns its material instance; the shell owns the selection.
        if (_appliedBackdrop != _backdrop)
        {
            _searchWindow.SystemBackdrop = _backdrop switch
            {
                BackdropKind.Mica => new Microsoft.UI.Xaml.Media.MicaBackdrop(),
                BackdropKind.MicaAlt => new Microsoft.UI.Xaml.Media.MicaBackdrop
                {
                    Kind = Microsoft.UI.Composition.SystemBackdrops.MicaKind.BaseAlt
                },
                BackdropKind.DesktopAcrylic => new Microsoft.UI.Xaml.Media.DesktopAcrylicBackdrop(),
                _ => null
            };
            _appliedBackdrop = _backdrop;
        }
        ApplyCaptionTheme();
    }

    private void ApplyCaptionTheme()
    {
        if (_root is null || _searchWindow is null) return;
        var dark = _root.ActualTheme == ElementTheme.Dark;
        var caption = _searchWindow.AppWindow.TitleBar;
        caption.ButtonForegroundColor = dark ? Microsoft.UI.Colors.White : Microsoft.UI.Colors.Black;
        caption.ButtonInactiveForegroundColor = dark ? Microsoft.UI.Colors.LightGray : Microsoft.UI.Colors.DimGray;
        caption.ButtonBackgroundColor = Microsoft.UI.Colors.Transparent;
        caption.ButtonInactiveBackgroundColor = Microsoft.UI.Colors.Transparent;
    }

    private void Report(string message)
    {
        if (_closed) return;
        if (string.IsNullOrWhiteSpace(message)) return;
        if (_status is not null) _status.Text = message;
        StackBehaviorNotification.Queue.Add(message, DateTimeOffset.UtcNow);
    }

    private async void LaunchFromMenu(InstalledApp app, bool elevated)
    {
        try
        {
            var result = await new WinApps.Services.Packages.ApplicationLauncher().LaunchAsync(app, elevated);
            if (!_closed) Report(result);
        }
        catch (Exception error)
        {
            WindowDiagnostics.Write("Compact launch", error);
            if (!_closed) Report($"Could not launch {app.Name}: {error.Message}");
        }
    }

    private void RefreshSuggestions()
    {
        if (_closed) return;
        if (_pendingOverview is not null) return;
        if (_search is null || _status is null || _badges is null || _root is null) return;
        var query = _search.Text.Trim();
        if (_select is null)
        {
            _status.Text = "Loading your application library…";
            RenderLoadingItems("Loading applications…");
            return;
        }
        _matches = query.Length > 0
            ? _apps.Where(app => app.Name.Contains(query, StringComparison.OrdinalIgnoreCase)
                || app.Publisher.Contains(query, StringComparison.OrdinalIgnoreCase)
                || app.FamilyName.Contains(query, StringComparison.OrdinalIgnoreCase))
                .OrderBy(app => app.Name, StringComparer.OrdinalIgnoreCase).ToArray()
            : _apps.Where(app => _activeKeys.Contains(app.FullName)).OrderBy(app => app.Name, StringComparer.OrdinalIgnoreCase)
                .Concat(_mostViewed.Length > 0 ? _mostViewed : _apps.OrderBy(app => app.Name, StringComparer.OrdinalIgnoreCase).Take(6))
                .DistinctBy(app => app.FullName).ToArray();
        _status.Text = _select is null ? "Loading your application library…"
            : _matches.Length == 0 ? "No matching applications."
            : query.Length > 0 ? $"{_matches.Length} matching applications"
            : _activeKeys.Count > 0 ? "Running & suggested apps · Choose an app to open its overview"
            : _mostViewed.Length > 0 ? "Suggested for you · Choose an app to open its overview" : "Suggested apps · Choose an app to open its overview";
        foreach (var tile in _activityTiles) tile.Stop();
        _activityTiles.Clear();
        _badges.Children.Clear();
        if (query.Length > 0) _exposureSignature = null;
        foreach (var app in _matches)
        {
            var content = new Grid { ColumnSpacing = 8 };
            content.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(24) });
            content.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            UIElement icon = new FontIcon { Glyph = "\uE71D", FontSize = 24, VerticalAlignment = VerticalAlignment.Center };
            if (app.IconUri is not null)
            {
                var uri = string.IsNullOrWhiteSpace(app.FamilyName) ? app.IconUri
                    : WinApps.Services.Icons.ApplicationIcons.NormalizeCachedPackageIcon(app.IconUri);
                if (Uri.TryCreate(uri, UriKind.Absolute, out var source))
                    icon = new Image { Source = new BitmapImage(source), Width = 24, Height = 24 };
            }
            content.Children.Add(icon);
            var name = new TextBlock { Text = app.Name, TextWrapping = TextWrapping.Wrap,
                VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(name, 1);
            content.Children.Add(name);
            var instanceCount = new TextBlock { FontSize = 10, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold };
            var instanceBadge = new Border
            {
                Child = instanceCount, Style = (Style)_root.Resources["InstanceBadge"],
                HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Bottom,
                Margin = new Thickness(0, 0, -5, -5), Visibility = Visibility.Collapsed
            };
            Grid.SetColumnSpan(instanceBadge, 2);
            content.Children.Add(instanceBadge);
            name.Margin = new Thickness(0, 0, 14, 0);
            var button = new Button { Content = content, Style = (Style)_root.Resources["SuggestionBadge"],
                HorizontalAlignment = HorizontalAlignment.Stretch, VerticalAlignment = VerticalAlignment.Stretch,
                MinHeight = 76, Margin = new Thickness(0), IsEnabled = _select is not null };
            Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(button, $"View {app.Name}");
            ToolTipService.SetToolTip(button, $"Open {app.Name} overview");
            button.ContextFlyout = ApplicationRail.CreateApplicationContextFlyout(app, Report, LaunchFromMenu, () => Open(app));
            button.Click += (_, _) => Open(app);
            _badges.Children.Add(button);
            _activityTiles.Add(new CompactActivityTile(app, button, instanceBadge, instanceCount));
        }
        LayoutBadges();
        _activity?.Refresh();
        var signature = string.Join("|", _matches.Select(app => app.FullName));
        if (_learningEnabled && query.Length == 0 && _select is not null && _compactMode && _searchWindow?.AppWindow.IsVisible == true
            && signature != _exposureSignature)
        {
            _exposureSignature = signature;
            _history?.RecordExposure(_matches, "compact");
        }
    }

    private void LayoutBadges()
    {
        if (_badges is null) return;
        _badges.ColumnDefinitions.Clear();
        _badges.RowDefinitions.Clear();
        for (var column = 0; column < _badgeColumns; column++)
            _badges.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        for (var row = 0; row < (_badges.Children.Count + _badgeColumns - 1) / _badgeColumns; row++)
            _badges.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        for (var index = 0; index < _badges.Children.Count; index++)
        {
            if (_badges.Children[index] is not FrameworkElement tile) continue;
            Grid.SetColumn(tile, index % _badgeColumns);
            Grid.SetRow(tile, index / _badgeColumns);
        }
    }

    private void RenderLoadingItems(string caption)
    {
        if (_badges is null || _root is null) return;
        foreach (var tile in _activityTiles) tile.Stop();
        _activityTiles.Clear();
        _badges.Children.Clear();
        for (var index = 0; index < 6; index++)
        {
            var template = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
            template.Children.Add(new ProgressRing { Width = 20, Height = 20, IsActive = index == 0 });
            var lines = new StackPanel { Spacing = 8, VerticalAlignment = VerticalAlignment.Center };
            if (index == 0) lines.Children.Add(new TextBlock { Text = caption, TextWrapping = TextWrapping.Wrap, MaxWidth = 110 });
            else
            {
                lines.Children.Add(new Border { Height = 8, Width = 80, Opacity = 0.25, Style = (Style)_root.Resources["LoadingLine"] });
                lines.Children.Add(new Border { Height = 6, Width = 52, Opacity = 0.15, Style = (Style)_root.Resources["LoadingLine"] });
            }
            template.Children.Add(lines);
            _badges.Children.Add(new Button { Content = template, Style = (Style)_root.Resources["SuggestionBadge"],
                MinHeight = 76, IsEnabled = false, IsTabStop = false });
        }
        LayoutBadges();
    }

    private void Open(InstalledApp app)
    {
        if (_closed) return;
        if (_pendingOverview is not null) return;
        if (_select is not { } select) return;
        var suggestion = string.IsNullOrWhiteSpace(_search?.Text);
        _pendingOverview = app.FullName;
        if (_search is not null) _search.IsEnabled = false;
        if (_status is not null) _status.Text = $"Loading {app.Name} overview…";
        RenderLoadingItems("Loading overview…");
        if (suggestion) _history?.RecordHit(app, "compact");
        try { select(app); }
        catch (Exception error)
        {
            _pendingOverview = null;
            if (_search is not null) _search.IsEnabled = true;
            RefreshSuggestions();
            Report($"Could not open {app.Name} overview: {error.Message}");
        }
    }

    private void RestoreMain()
    {
        if (_closed) return;
        _pendingOverview = null;
        if (_search is not null) _search.IsEnabled = true;
        RefreshSuggestions();
        SaveCompactPlacement();
        _compactMode = false;
        _searchWindow?.AppWindow.Hide();
        if (_searchWindow is not null) _searchWindow.AppWindow.IsShownInSwitchers = false;
        _main.AppWindow.Show();
        var maximize = _wasMaximized;
        if (_main.AppWindow.Presenter is OverlappedPresenter { State: OverlappedPresenterState.Minimized } presenter)
        {
            presenter.Restore();
            if (maximize) presenter.Maximize();
        }
        _main.Activate();
    }

    private void MainClosed(object sender, WindowEventArgs args)
    {
        if (_closed) return;
        try { SaveCompactPlacement(); }
        catch (Exception error) { WindowDiagnostics.Write("Compact placement on close", error); }
        _closed = true;
        _placementTimer.Stop();
        _placementTimer.Tick -= PlacementTick;
        Cleanup(() => _activity?.Dispose());
        _activity = null;
        if (_companionAppWindow is { } companionApp)
        {
            Cleanup(() => companionApp.Changed -= CompactChanged);
            Cleanup(() => companionApp.Closing -= CompactClosing);
        }
        if (_search is not null) _search.TextChanged -= SearchTextChanged;
        if (_badges is not null) _badges.SizeChanged -= BadgesSizeChanged;
        if (_root is not null) _root.ActualThemeChanged -= CompactThemeChanged;
        foreach (var tile in _activityTiles) Cleanup(tile.Stop);
        _activityTiles.Clear();
        if (_badges is not null)
        {
            foreach (var button in _badges.Children.OfType<Button>())
            {
                Cleanup(() => button.ContextFlyout?.Hide());
                button.ContextFlyout = null;
                button.Content = null;
                ToolTipService.SetToolTip(button, null);
            }
            _badges.Children.Clear();
        }
        _select = null;
        _pendingOverview = null;
        _history = null;
        _apps = []; _mostViewed = []; _matches = [];
        Cleanup(() => _mainAppWindow.Changed -= MainChanged);
        _main.Closed -= MainClosed;
        if (_searchWindow is { } closingWindow)
        {
            Cleanup(() => closingWindow.SetTitleBar(null));
            Cleanup(() => closingWindow.Content = null);
        }
        try { _searchWindow?.Close(); }
        catch (Exception error) { WindowDiagnostics.Write("Compact close", error); }
        _searchWindow = null;
        _companionAppWindow = null;
        _root = null; _search = null; _status = null; _badges = null;
        Controllers.Remove(_main);
    }

    private static void Cleanup(Action release)
    {
        try { release(); }
        catch (Exception error) { WindowDiagnostics.Write("Compact resource cleanup", error); }
    }
}
