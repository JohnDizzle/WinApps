// SPDX-License-Identifier: GPL-3.0-only
using System.Collections.ObjectModel;
using System.Diagnostics;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Markup;
using WinApps.Services.Inventory;
using Windows.ApplicationModel.DataTransfer;

namespace WinApps.Browser;

// A native companion keeps the virtualized stream stable while Reactor updates
// the application cards underneath it, just as Mini Mode retains its controls.
internal sealed class WindowInspectorWindow
{
    private static readonly Dictionary<(Window Owner, int Pid, long Started), WindowInspectorWindow> OpenInspectors = [];
    private readonly Window _window;
    private readonly RunningApplication _app;
    private readonly Window _owner;
    private readonly ComboBox _windows = new() { Header = "Window to inspect", HorizontalAlignment = HorizontalAlignment.Stretch, MinWidth = 180, MaxDropDownHeight = 300 };
    private readonly CheckBox _advancedWindows = new() { Content = "Include child and hidden windows (advanced)", FontSize = 12 };
    private readonly TextBlock _windowSummary = new() { TextWrapping = TextWrapping.Wrap, FontSize = 12 };
    private readonly TextBlock _identity = new() { TextWrapping = TextWrapping.Wrap, FontSize = 12 };
    private readonly TextBlock _metrics = new() { TextWrapping = TextWrapping.Wrap, FontSize = 13 };
    private readonly TextBlock _details = new() { IsTextSelectionEnabled = true, TextWrapping = TextWrapping.Wrap, FontSize = 13 };
    private readonly TextBlock _process = new() { IsTextSelectionEnabled = true, TextWrapping = TextWrapping.Wrap, FontSize = 13 };
    private readonly TextBlock _status = new() { TextWrapping = TextWrapping.Wrap, FontSize = 12 };
    private readonly TextBox _filter = new() { Header = "Search events", PlaceholderText = "Event name, handle or thread", Width = 240 };
    private readonly ComboBox _eventType = new() { Header = "Event type", Width = 180 };
    private readonly ComboBox _scope = new() { Header = "Show events from", Width = 200, ItemsSource = new[] { "All app windows", "Selected window only" }, SelectedIndex = 0 };
    private readonly TextBlock _streamTitle = new() { Text = "Live events · All app windows", FontSize = 18, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold };
    private readonly TextBlock _emptyTitle = new() { Text = "Listening for window activity", FontSize = 20, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, TextAlignment = TextAlignment.Center };
    private readonly TextBlock _emptyHelp = new() { TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Center, MaxWidth = 470 };
    private readonly StackPanel _empty = new() { Spacing = 10, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, IsHitTestVisible = false };
    private readonly CheckBox _follow = new() { Content = "Follow latest", IsChecked = true, VerticalAlignment = VerticalAlignment.Center };
    private readonly CheckBox _controlEvents = new() { Content = "Capture detailed control-object events (advanced)", FontSize = 12 };
    private readonly ListView _events = new() { SelectionMode = ListViewSelectionMode.None, IsItemClickEnabled = false, FontFamily = new FontFamily("Consolas"), FontSize = 12 };
    private readonly ObservableCollection<WindowEventRow> _visible = [];
    private readonly Queue<WindowEventEntry> _history = new();
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(100) };
    private WindowEventStream? _stream;
    private InspectedWindow? _selected;
    private bool _closed;
    private int _ticks;
    private long _evicted;
    private long _overflowDropped;
    private string? _error;
    private string? _actionNotice;

    internal static Window Show(Window owner, RunningApplication app, bool activate = true)
    {
        var key = (owner, app.Id, app.Started);
        if (OpenInspectors.TryGetValue(key, out var existing)) { if (activate) existing._window.Activate(); return existing._window; }
        var inspector = new WindowInspectorWindow(owner, app);
        OpenInspectors.Add(key, inspector);
        if (activate) inspector._window.Activate();
        return inspector._window;
    }

    private WindowInspectorWindow(Window owner, RunningApplication app)
    {
        _owner = owner;
        _app = app;
        _window = new Window { Title = $"{app.Name} — Window Inspector · WinApps" };
        var scale = NativeMonitor.WindowScale(owner);
        var work = NativeMonitor.WorkArea(owner.AppWindow.Position.X, owner.AppWindow.Position.Y);
        var width = Math.Min(work.Width, (int)(1100 * scale));
        var height = Math.Min(work.Height, (int)(850 * scale));
        _window.AppWindow.MoveAndResize(new Windows.Graphics.RectInt32(work.X + (work.Width - width) / 2,
            work.Y + (work.Height - height) / 2, width, height));
        if (_window.AppWindow.Presenter is Microsoft.UI.Windowing.OverlappedPresenter presenter)
        {
            presenter.PreferredMinimumWidth = Math.Min(work.Width, (int)(760 * scale));
            presenter.PreferredMinimumHeight = Math.Min(work.Height, (int)(620 * scale));
        }
        _window.AppWindow.SetIcon(Path.Combine(AppContext.BaseDirectory, "Assets", "WinApps.ico"));
        var root = new Grid { Padding = new Thickness(20), RowSpacing = 12,
            RequestedTheme = (owner.Content as FrameworkElement)?.RequestedTheme ?? ElementTheme.Default,
            Background = (Brush)Microsoft.UI.Xaml.Application.Current.Resources["ApplicationPageBackgroundThemeBrush"] };
        for (var i = 0; i < 5; i++) root.RowDefinitions.Add(new RowDefinition { Height = i == 3 ? new GridLength(1, GridUnitType.Star) : GridLength.Auto });
        var heading = new Grid { ColumnSpacing = 16 };
        heading.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        heading.ColumnDefinitions.Add(new ColumnDefinition());
        var appIcon = new Border { Width = 56, Height = 56, CornerRadius = new CornerRadius(12), VerticalAlignment = VerticalAlignment.Center,
            Background = new SolidColorBrush(Windows.UI.Color.FromArgb(24, 128, 128, 128)), Child = new SymbolIcon(Symbol.AllApps) };
        if (Uri.TryCreate(app.IconUri, UriKind.Absolute, out var iconUri))
        {
            var image = new Image { Width = 40, Height = 40, Source = new BitmapImage(iconUri) };
            image.ImageFailed += (_, _) => appIcon.Child = new SymbolIcon(Symbol.AllApps);
            appIcon.Child = image;
        }
        heading.Children.Add(appIcon);
        var brand = new StackPanel { Spacing = 3 };
        brand.Children.Add(new TextBlock { Text = "WinApps  /  WINDOW INSPECTOR", FontSize = 11, Opacity = 0.7 });
        brand.Children.Add(new TextBlock { Text = app.Name, FontSize = 24, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, TextTrimming = TextTrimming.CharacterEllipsis });
        _identity.Text = ReadIdentity(app);
        brand.Children.Add(_identity);
        brand.Children.Add(_metrics);
        Grid.SetColumn(brand, 1); heading.Children.Add(brand);
        Add(root, heading, 0);
        var windowPanel = new StackPanel { Spacing = 6 };
        var selector = new Grid { ColumnSpacing = 8 };
        selector.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        selector.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        selector.Children.Add(_windows);
        var refresh = ActionButton("Refresh windows", RefreshWindows);
        refresh.VerticalAlignment = VerticalAlignment.Bottom;
        Grid.SetColumn(refresh, 1); selector.Children.Add(refresh);
        windowPanel.Children.Add(selector);
        windowPanel.Children.Add(_windowSummary);
        windowPanel.Children.Add(_advancedWindows);
        var information = new Grid { ColumnSpacing = 24, MaxHeight = 180 };
        information.ColumnDefinitions.Add(new ColumnDefinition()); information.ColumnDefinitions.Add(new ColumnDefinition());
        information.Children.Add(new ScrollViewer { Content = _details, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
        var processScroll = new ScrollViewer { Content = _process, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        Grid.SetColumn(processScroll, 1); information.Children.Add(processScroll);
        windowPanel.Children.Add(new Expander { Header = "Window & process details", Content = information,
            HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Stretch });
        Add(root, Surface(windowPanel), 1);
        var tools = new StackPanel { Spacing = 8 };
        var actions = new Grid { ColumnSpacing = 12 };
        actions.ColumnDefinitions.Add(new ColumnDefinition());
        actions.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        _streamTitle.VerticalAlignment = VerticalAlignment.Center;
        actions.Children.Add(_streamTitle);
        var toolbar = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        var pause = new Button { Content = "Pause" };
        pause.Click += (_, _) =>
        {
            if (_stream is null) return;
            _stream.Paused = !_stream.Paused;
            pause.Content = _stream.Paused ? "Resume" : "Pause";
            UpdateStatus();
        };
        toolbar.Children.Add(pause);
        toolbar.Children.Add(ActionButton("Clear", () => { _history.Clear(); _visible.Clear(); _stream?.Clear(); _evicted = 0; _overflowDropped = 0; UpdateStatus(); }));
        toolbar.Children.Add(ActionButton("Copy log", CopyLog));
        toolbar.Children.Add(ActionButton("Export…", ExportLog));
        Grid.SetColumn(toolbar, 1); actions.Children.Add(toolbar);
        tools.Children.Add(actions);
        _eventType.ItemsSource = new[] { "All event types" }.Concat(WindowEventStream.Names.Values.Distinct().Order()).ToArray();
        _eventType.SelectedIndex = 0;
        var filters = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 };
        filters.Children.Add(_scope); filters.Children.Add(_eventType); filters.Children.Add(_filter); filters.Children.Add(_follow);
        tools.Children.Add(new ScrollViewer { Content = filters, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollMode = ScrollMode.Enabled, VerticalScrollMode = ScrollMode.Disabled });
        ToolTipService.SetToolTip(_controlEvents, "Off by default to reduce overhead. Enables events for accessibility objects inside windows; these can be very frequent. This changes capture, not just the displayed filter.");
        tools.Children.Add(_controlEvents);
        Add(root, tools, 2);
        _events.ItemsSource = _visible;
        _events.ItemContainerStyle = (Style)XamlReader.Load("""
            <Style xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" TargetType="ListViewItem">
                <Setter Property="HorizontalContentAlignment" Value="Stretch" />
                <Setter Property="Padding" Value="4,9" />
            </Style>
            """);
        _events.ItemTemplate = (DataTemplate)XamlReader.Load("""
            <DataTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation">
                <Grid ColumnSpacing="12">
                    <Grid.ColumnDefinitions>
                        <ColumnDefinition Width="92" /><ColumnDefinition Width="155" />
                        <ColumnDefinition Width="*" /><ColumnDefinition Width="175" />
                    </Grid.ColumnDefinitions>
                    <TextBlock Text="{Binding Time}" Opacity="0.7" FontSize="12" />
                    <TextBlock Grid.Column="1" Text="{Binding Event}" FontWeight="SemiBold" FontSize="13" TextWrapping="Wrap" />
                    <TextBlock Grid.Column="2" Text="{Binding Title}" TextWrapping="Wrap" MaxLines="2" TextTrimming="CharacterEllipsis" FontFamily="Segoe UI" FontSize="13" ToolTipService.ToolTip="{Binding Title}" />
                    <StackPanel Grid.Column="3" Spacing="3">
                        <TextBlock Text="{Binding Handle}" FontSize="12" />
                        <TextBlock Text="{Binding Details}" FontSize="10" Opacity="0.65" TextWrapping="Wrap" />
                    </StackPanel>
                </Grid>
            </DataTemplate>
            """);
        var streamArea = new Grid();
        streamArea.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        streamArea.RowDefinitions.Add(new RowDefinition());
        var columnHeaders = new Grid { ColumnSpacing = 12, Padding = new Thickness(4, 0, 4, 10), Opacity = 0.7 };
        foreach (var columnWidth in new[] { new GridLength(92), new GridLength(155), new GridLength(1, GridUnitType.Star), new GridLength(175) })
            columnHeaders.ColumnDefinitions.Add(new ColumnDefinition { Width = columnWidth });
        var labels = new[] { "TIME", "EVENT", "WINDOW TITLE", "HANDLE / THREAD" };
        for (var index = 0; index < labels.Length; index++)
        {
            var label = new TextBlock { Text = labels[index], FontSize = 11 };
            Grid.SetColumn(label, index); columnHeaders.Children.Add(label);
        }
        streamArea.Children.Add(columnHeaders);
        Grid.SetRow(_events, 1); streamArea.Children.Add(_events);
        _empty.Children.Add(_emptyTitle); _empty.Children.Add(_emptyHelp);
        Grid.SetRow(_empty, 1); streamArea.Children.Add(_empty);
        Add(root, Surface(streamArea), 3); Add(root, _status, 4);
        _window.Content = root;
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(_windows, "Application windows and child windows");
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(_filter, "Filter live events");
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(_events, "Live window event stream");
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(_eventType, "Filter by event type");
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(_scope, "Filter events by window");
        ToolTipService.SetToolTip(_eventType, "Shows only this event type in the retained log. It does not change the inspected application.");
        ToolTipService.SetToolTip(_scope, "Selected window only matches its exact handle. All app windows includes child windows.");
        _filter.TextChanged += (_, _) => RebuildVisible();
        _eventType.SelectionChanged += (_, _) => RebuildVisible();
        _scope.SelectionChanged += (_, _) => RebuildVisible();
        _advancedWindows.Checked += (_, _) => RefreshWindows();
        _advancedWindows.Unchecked += (_, _) => RefreshWindows();
        _controlEvents.Checked += (_, _) => { if (_stream is not null) _stream.IncludeControlObjects = true; };
        _controlEvents.Unchecked += (_, _) => { if (_stream is not null) _stream.IncludeControlObjects = false; };
        _windows.SelectionChanged += (_, _) => { _selected = _windows.SelectedItem as InspectedWindow; RefreshDetails(); RebuildVisible(); };
        _window.Closed += Closed;
        _owner.Closed += OwnerClosed;
        _timer.Tick += Tick;
        if (IsSameProcess())
        {
            RefreshWindows();
            try { _stream = new WindowEventStream(app.Id, representative: ApplicationWindowSwitcher.HasOpenWindow(app) ? app.WindowHandle : 0,
                excludedWindow: WinRT.Interop.WindowNative.GetWindowHandle(_window)); }
            catch (Exception error) { _error = error.Message; }
            _timer.Start();
        }
        else _error = "The original process has exited or is unavailable. Reopen the inspector from a current live card.";
        UpdateStatus();
    }

    private static Button ActionButton(string label, Action action)
    {
        var button = new Button { Content = label };
        button.Click += (_, _) => action();
        return button;
    }

    private static void Add(Grid grid, FrameworkElement child, int row) { Grid.SetRow(child, row); grid.Children.Add(child); }

    private static Border Surface(UIElement content) => new() { Child = content, Padding = new Thickness(12), CornerRadius = new CornerRadius(10),
        BorderThickness = new Thickness(1), BorderBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(50, 128, 128, 128)),
        Background = new SolidColorBrush(Windows.UI.Color.FromArgb(10, 128, 128, 128)) };

    private static string ReadIdentity(RunningApplication app)
    {
        try
        {
            var info = FileVersionInfo.GetVersionInfo(app.Path);
            var identity = new[] { info.CompanyName, info.ProductVersion }.Where(value => !string.IsNullOrWhiteSpace(value));
            return string.Join(" · ", identity.Append($"PID {app.Id}"));
        }
        catch (Exception) { return $"PID {app.Id} · Application metadata unavailable"; }
    }

    private bool IsSameProcess()
    {
        try { using var process = Process.GetProcessById(_app.Id); return !process.HasExited && process.StartTime.ToUniversalTime().Ticks == _app.Started; }
        catch (Exception) { return false; }
    }

    private void RefreshWindows()
    {
        if (!IsSameProcess()) { _details.Text = "The original process has exited or is unavailable."; return; }
        // Never rediscover an unrelated window through a reused representative HWND.
        var representative = ApplicationWindowSwitcher.HasOpenWindow(_app) ? _app.WindowHandle : 0;
        var all = WindowInspection.Enumerate(_app.Id, representative);
        var windows = (_advancedWindows.IsChecked == true ? all : all.Where(item => item.Depth == 0 && item.Visible && !string.IsNullOrWhiteSpace(item.Title)))
            .OrderByDescending(item => item.Handle == representative).ToArray();
        var previous = _selected;
        _windows.ItemsSource = windows;
        _windows.SelectedItem = windows.FirstOrDefault(item => item.Handle == (previous?.Handle ?? _app.WindowHandle)
            && (previous is null || item.ThreadId == previous.ThreadId)) ?? windows.FirstOrDefault();
        _windows.Header = $"Window to inspect · {windows.Length:N0} {(_advancedWindows.IsChecked == true ? "windows / controls" : "visible main windows")}";
        if (windows.Length == 0) { _selected = null; _details.Text = "No visible main windows. Include child and hidden windows to inspect internal controls."; }
        RefreshDetails();
    }

    private void RefreshDetails()
    {
        if (_selected is not null) _details.Text = WindowInspection.Describe(_selected);
        _windowSummary.Text = _selected is null ? "No window selected. Include child and hidden windows to explore internal controls." :
            $"{(_selected.Visible ? "Visible" : "Hidden")} {(_selected.Depth > 0 ? "child control" : "top-level window")} · Handle 0x{_selected.Handle:X} · Expand details for class, bounds and styles.";
        try
        {
            using var process = Process.GetProcessById(_app.Id);
            if (process.StartTime.ToUniversalTime().Ticks != _app.Started) return;
            _metrics.Text = $"Running · {process.WorkingSet64 / 1048576d:N0} MiB memory · {process.Threads.Count:N0} threads · Started {process.StartTime:t}";
            _process.Text = $"Process: {_app.Name}\nPID: {_app.Id}\nStarted: {process.StartTime:g}\n" +
                $"Working set: {process.WorkingSet64 / 1048576d:N1} MiB\nPrivate memory: {process.PrivateMemorySize64 / 1048576d:N1} MiB\n" +
                $"CPU time: {process.TotalProcessorTime:g}\nThreads: {process.Threads.Count}\nExecutable: {(_app.Path.Length > 0 ? _app.Path : "Access unavailable")}";
        }
        catch (Exception) { _process.Text = "Process details unavailable. The process may have exited or access may be restricted."; }
    }

    private void Tick(object? sender, object args)
    {
        if (_closed) return;
        // Bound work per frame as well as memory usage during event storms.
        var entries = _stream?.Drain(100) ?? [];
        if (_stream?.Error is { } captureError) _error = $"Monitoring stopped: {captureError}";
        _overflowDropped = _stream?.Dropped ?? _overflowDropped;
        var filter = _filter.Text;
        foreach (var entry in entries)
        {
            if (_history.Count == WindowEventStream.Capacity)
            {
                var removed = _history.Dequeue(); _evicted++;
                if (Matches(removed, filter) && _visible.Count > 0) _visible.RemoveAt(0);
            }
            _history.Enqueue(entry);
            if (Matches(entry, filter)) _visible.Add(new WindowEventRow(entry));
        }
        if (entries.Length > 0 && _follow.IsChecked == true && _visible.Count > 0) _events.ScrollIntoView(_visible[^1]);
        if (++_ticks % 10 == 0)
        {
            if (!IsSameProcess())
            {
                _stream?.Dispose(); _stream = null; _timer.Stop();
                _error = "The original process exited or became unavailable. Monitoring stopped; the captured log remains available.";
            }
            else RefreshDetails();
        }
        UpdateStatus();
    }

    private bool Matches(WindowEventEntry entry, string filter) =>
        (_eventType.SelectedIndex <= 0 || entry.Name == _eventType.SelectedItem as string) &&
        (_scope.SelectedIndex != 1 || entry.Window == _selected?.Handle) &&
        entry.ToString().Contains(filter, StringComparison.OrdinalIgnoreCase);
    private void RebuildVisible()
    {
        _visible.Clear();
        foreach (var entry in _history.Where(item => Matches(item, _filter.Text))) _visible.Add(new WindowEventRow(entry));
        UpdateStatus();
    }

    private void UpdateStatus()
    {
        _streamTitle.Text = $"Live events · {(_scope.SelectedIndex == 1 ? "Selected window" : "All app windows")}";
        _empty.Visibility = _visible.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        var paused = _stream?.Paused == true;
        var starting = _stream is not null && !_stream.Ready.IsCompleted;
        _emptyTitle.Text = _error is not null ? "Monitoring unavailable" : starting ? "Starting window capture" : paused ? "Event stream paused" : _history.Count > 0 ? "No events match your filters" : "Listening for window activity";
        _emptyHelp.Text = _error ?? (paused ? "Choose Resume to receive new events. Activity during pause is not recorded." : _history.Count > 0
            ? "Choose All event types or All app windows, or clear Search events to see more of the captured activity."
            : $"Switch to {_app.Name} and click, move, resize or minimize one of its windows. New events will appear here automatically. Earlier activity is not replayed.");
        _status.Text = (_actionNotice is null ? "" : _actionNotice + "\n") + (_error ??
            $"{(starting ? "Starting capture" : paused ? "Paused · incoming events omitted" : "● Listening")} · {_visible.Count:N0} shown / {_history.Count:N0} retained · {_evicted:N0} expired · {_overflowDropped:N0} dropped\n" +
            "Filters change this view only. Copy / export saves the filtered log. Latest 2,000 Windows events retained.");
    }

    private string LogText() => $"WinApps Window Inspector · {_app.Name} · PID {_app.Id} · Started {new DateTime(_app.Started, DateTimeKind.Utc):O}\nCaptured/exported {DateTimeOffset.Now:O} · Search: {_filter.Text} · Type: {_eventType.SelectedItem} · Scope: {_scope.SelectedItem} · Selected HWND: 0x{_selected?.Handle ?? 0:X}\n" +
        $"Expired: {_evicted}; overflow drops: {_overflowDropped}\n" + string.Join(Environment.NewLine, _visible);

    private void CopyLog()
    {
        try { var data = new DataPackage(); data.SetText(LogText()); Clipboard.SetContent(data); _actionNotice = "Filtered log copied."; }
        catch (Exception error) { _actionNotice = $"Could not copy log: {error.Message}"; }
        UpdateStatus();
    }

    private async void ExportLog()
    {
        var text = LogText();
        try
        {
            var picker = new Windows.Storage.Pickers.FileSavePicker { SuggestedFileName = $"WinApps-events-{_app.Id}" };
            picker.FileTypeChoices.Add("Text log", new List<string> { ".txt" });
            WinRT.Interop.InitializeWithWindow.Initialize(picker, WinRT.Interop.WindowNative.GetWindowHandle(_window));
            var file = await picker.PickSaveFileAsync();
            if (file is not null)
            {
                await Windows.Storage.FileIO.WriteTextAsync(file, text);
                if (!_closed) { _actionNotice = "Filtered log exported."; UpdateStatus(); }
            }
        }
        catch (Exception error) { if (!_closed) { _actionNotice = $"Could not export log: {error.Message}"; UpdateStatus(); } }
    }

    private void OwnerClosed(object sender, WindowEventArgs args) => _window.Close();
    private void Closed(object sender, WindowEventArgs args)
    {
        _closed = true;
        _timer.Stop(); _timer.Tick -= Tick;
        _stream?.Dispose(); _stream = null;
        _owner.Closed -= OwnerClosed;
        OpenInspectors.Remove((_owner, _app.Id, _app.Started));
        _window.Content = null;
    }
}
