// SPDX-License-Identifier: GPL-3.0-only
using Microsoft.UI.Xaml;
using Microsoft.UI.Reactor;
using Microsoft.UI.Reactor.Core;
using WinApps.Models;
using WinApps.Services.Inventory;
using static Microsoft.UI.Reactor.Factories;
using static WinApps.Browser.BrowserUi;

namespace WinApps.Browser;

internal sealed record ApplicationActivityPanelProps(InstalledApp? App = null, Action<string>? OnSummary = null,
    InstalledApp[]? Apps = null, Action<InstalledApp>? OnSelect = null, RunningApplication[]? Snapshot = null,
    Func<IReadOnlySet<int>?, CancellationToken, RunningApplication[]>? ReadWindows = null,
    Func<RunningApplication, bool>? WindowOpen = null, Action<InstalledApp, RunningApplication[]>? OnSelectActivity = null);

internal sealed class ApplicationActivityPanel : Component<ApplicationActivityPanelProps>
{
    private Microsoft.UI.Xaml.Controls.ScrollView? _carousel;
    private RunningApplication[] _snapshot = [];
    private readonly HashSet<(int Id, long Started)> _newApps = [];
    private bool _revealNewCards;
    private sealed class Acknowledgement { public Action? Invoke; }
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<FrameworkElement, Acknowledgement> Acknowledgements = new();
    private static void AttachAcknowledgement(FrameworkElement control, Action acknowledge)
    {
        if (!Acknowledgements.TryGetValue(control, out var action))
        {
            action = new Acknowledgement();
            Acknowledgements.Add(control, action);
            control.AddHandler(UIElement.TappedEvent, new Microsoft.UI.Xaml.Input.TappedEventHandler((_, _) => action.Invoke?.Invoke()), true);
        }
        action.Invoke = acknowledge;
    }
    private sealed class CardAction { public Action? Open; public bool Opening; }
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<FrameworkElement, CardAction> CardActions = new();
    private static void AttachOpen(FrameworkElement control, Action? open)
    {
        if (CardActions.TryGetValue(control, out var action)) { action.Open = open; return; }
        action = new CardAction { Open = open };
        CardActions.Add(control, action);
        control.Tapped += (_, args) =>
        {
            var source = args.OriginalSource as DependencyObject;
            while (source is not null && source != control)
            {
                if (source is Microsoft.UI.Xaml.Controls.Primitives.ButtonBase) return;
                source = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetParent(source);
            }
            if (action.Open is null || action.Opening) return;
            action.Opening = true;
            BrowserMotion.CardSelectionFeedback(control, true);
            if (control is Microsoft.UI.Xaml.Controls.Border { Child: Microsoft.UI.Xaml.Controls.StackPanel content })
            {
                var indicator = new Microsoft.UI.Xaml.Controls.StackPanel
                {
                    Orientation = Microsoft.UI.Xaml.Controls.Orientation.Horizontal,
                    Spacing = 8,
                    HorizontalAlignment = HorizontalAlignment.Center
                };
                indicator.Children.Add(new Microsoft.UI.Xaml.Controls.ProgressRing { IsActive = true, Width = 18, Height = 18 });
                indicator.Children.Add(new Microsoft.UI.Xaml.Controls.TextBlock { Text = "Loading application…", VerticalAlignment = VerticalAlignment.Center });
                content.Children.Add(indicator);
                Microsoft.UI.Xaml.Automation.AutomationProperties.SetLiveSetting(indicator,
                    Microsoft.UI.Xaml.Automation.Peers.AutomationLiveSetting.Polite);
            }
            args.Handled = true;
            // Publish the pressed/loading visuals before starting application selection.
            control.DispatcherQueue.TryEnqueue(() => action.Open?.Invoke());
        };
    }

    public override Element Render()
    {
        var entries = UseState(Props.Snapshot ?? Array.Empty<RunningApplication>());
        var loading = UseState(Props.Snapshot is null);
        var pending = UseState<RunningApplication?>(null);
        var message = UseState("");
        var refreshVersion = UseState(0);
        UseEffect(() =>
        {
            if (Props.Snapshot is not null) return () => { };
            var dispatcher = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();
            var cancellation = new CancellationTokenSource();
            var token = cancellation.Token;
            var disposed = false;
            var reading = false;
            RunningApplication[] ReadWindows(IReadOnlySet<int>? excluded = null) => Props.ReadWindows is not null
                ? Props.ReadWindows(excluded, token) : RunningApplications.Read(Props.App, cancellationToken: token, excludedIds: excluded);
            bool WindowOpen(RunningApplication item) => Props.WindowOpen?.Invoke(item) ?? ApplicationWindowSwitcher.HasOpenWindow(item);
            var measuredAt = DateTime.Now;
            void PublishSummary() => Props.OnSummary?.Invoke($"{_snapshot.Length} windowed apps · {_snapshot.Sum(item => item.Memory) / 1048576d:N0} MiB RAM snapshots · Updated {measuredAt:t}");
            void Acknowledge()
            {
                if (_newApps.Count == 0) return;
                _newApps.Clear();
                _revealNewCards = false;
                entries.Set(_snapshot.ToArray());
            }
            BrowserMotion.ActivityListAcknowledged += Acknowledge;
            async void ExpireHighlight(RunningApplication[] additions)
            {
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(3));
                    if (token.IsCancellationRequested) return;
                    dispatcher.TryEnqueue(() =>
                    {
                        if (disposed) return;
                        var changed = false;
                        foreach (var item in additions)
                            changed |= _newApps.Remove((item.Id, item.Started));
                        if (changed) entries.Set(_snapshot.ToArray());
                    });
                }
                catch (OperationCanceledException) { }
            }
            async void Refresh()
            {
                if (reading) return;
                reading = true;
                try
                {
                    var result = await Task.Run(() => ReadWindows());
                    if (token.IsCancellationRequested) return;
                    dispatcher.TryEnqueue(() =>
                    {
                        if (disposed) return;
                        _snapshot = result;
                        measuredAt = DateTime.Now;
                        entries.Set(result); loading.Set(false);
                        PublishSummary();
                    });
                }
                catch (OperationCanceledException) { }
                catch (Exception error) { dispatcher.TryEnqueue(() => { if (!disposed) { message.Set($"Activity unavailable: {error.Message}"); loading.Set(false); } }); }
                finally { reading = false; }
            }
            var windowTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
            async void CheckWindows(object? sender, object args)
            {
                if (disposed || reading) return;
                reading = true;
                var remaining = _snapshot.Where(WindowOpen).ToArray();
                try
                {
                    var knownIds = remaining.Select(item => item.Id).ToHashSet();
                    var found = await Task.Run(() => ReadWindows(knownIds));
                    dispatcher.TryEnqueue(() =>
                    {
                        reading = false;
                        if (disposed) return;
                        var additions = found.Where(WindowOpen).OrderByDescending(item => item.Started).ToArray();
                        if (remaining.Length == _snapshot.Length && additions.Length == 0) return;
                        foreach (var item in additions) _newApps.Add((item.Id, item.Started));
                        _snapshot = additions.Concat(remaining).ToArray();
                        _newApps.RemoveWhere(key => !_snapshot.Any(item => (item.Id, item.Started) == key));
                        measuredAt = DateTime.Now;
                        _revealNewCards = additions.Length > 0;
                        entries.Set(_snapshot);
                        PublishSummary();
                        if (additions.Length > 0) ExpireHighlight(additions);
                    });
                }
                catch (Exception) { dispatcher.TryEnqueue(() => { reading = false; }); }
            }
            windowTimer.Tick += CheckWindows;
            windowTimer.Start();
            Refresh();
            return () => { disposed = true; BrowserMotion.ActivityListAcknowledged -= Acknowledge; windowTimer.Stop(); windowTimer.Tick -= CheckWindows; cancellation.Cancel(); cancellation.Dispose(); };
        }, Props.App?.FullName, refreshVersion.Value);

        var items = entries.Value;
        void AcknowledgeNewApps()
        {
            if (_newApps.Count == 0) return;
            _newApps.Clear();
            _revealNewCards = false;
            entries.Set(items.ToArray());
        }
        InstalledApp? Match(RunningApplication item) => RunningApplicationMatch.Find(item.Path, item.Name, Props.Apps ?? []);
        void OpenOverview(RunningApplication item)
        {
            var app = Match(item);
            if (app is null) return;
            if (Props.OnSelectActivity is not null)
                Props.OnSelectActivity(app, items.Where(other => Match(other)?.FullName == app.FullName).ToArray());
            else Props.OnSelect?.Invoke(app);
        }
        void MoveCarousel(int direction)
        {
            if (_carousel is null) return;
            _carousel.ScrollTo(Math.Clamp(_carousel.HorizontalOffset + direction * 316, 0, _carousel.ScrollableWidth), 0,
                new Microsoft.UI.Xaml.Controls.ScrollingScrollOptions(
                    new Windows.UI.ViewManagement.UISettings().AnimationsEnabled
                        ? Microsoft.UI.Xaml.Controls.ScrollingAnimationMode.Enabled : Microsoft.UI.Xaml.Controls.ScrollingAnimationMode.Disabled,
                    Microsoft.UI.Xaml.Controls.ScrollingSnapPointsMode.Ignore));
        }
        Element ProcessCard(RunningApplication item, bool terminate) => ActivityCardStyle.Surface(VStack(8,
            ActivityCardStyle.AccentLine(),
            HStack(12,
                Border(item.IconUri is null ? FluentGlyph("\uE71D", 28).Width(40).Height(40)
                    : Image(item.IconUri).Width(40).Height(40)).Padding(4)
                    .Background(Theme.Ref("ControlFillColorSecondaryBrush")).WithBorder(Theme.CardStroke).CornerRadius(12),
                TextBlock(item.Name).FontSize(17).SemiBold().TextWrapping()
                    .Set(control => { control.MaxLines = 2; control.TextTrimming = TextTrimming.CharacterEllipsis; }).Width(208)),
            TextBlock(item.Title.Length > 0 ? item.Title : "Background process").Height(20)
                .Foreground(Theme.SecondaryText).Set(control =>
                {
                    control.TextTrimming = TextTrimming.CharacterEllipsis;
                    Microsoft.UI.Xaml.Controls.ToolTipService.SetToolTip(control, item.Title);
                }),
            Border(VStack(4,
                TextBlock($"{item.Memory / 1048576d:N1} MiB").FontSize(24).SemiBold()
                    .Foreground(Theme.Ref("AccentTextFillColorPrimaryBrush")),
                TextBlock("RAM · current working set").FontSize(12).Foreground(Theme.SecondaryText)))
                .Padding(10).Background(Theme.Ref("ControlFillColorSecondaryBrush")).CornerRadius(10),
            ActivityCardStyle.Badge(_newApps.Contains((item.Id, item.Started)) ? "NEW · SNAPSHOT" : "SNAPSHOT").HAlign(HorizontalAlignment.Left),
            TextBlock($"PID {item.Id}  ·  {item.Threads:N0} threads")
                .FontSize(12).TextWrapping().Foreground(Theme.SecondaryText)
                .Set(control => Microsoft.UI.Xaml.Controls.ToolTipService.SetToolTip(control,
                    $"Started {new DateTime(item.Started, DateTimeKind.Utc).ToLocalTime():g}")),
            FlexRow(
            GlassButton(Button(Image(new Uri(System.IO.Path.Combine(AppContext.BaseDirectory, "Assets", "Square44x44Logo.targetsize-24.png")).AbsoluteUri).Width(18).Height(18),
                () => OpenOverview(item)))
                .AutomationName($"Open {item.Name} overview in WinApps").Width(30).Height(30).Padding(0).CornerRadius(7)
                .Set(control => Microsoft.UI.Xaml.Controls.ToolTipService.SetToolTip(control, $"Open {Match(item)?.Name ?? item.Name} overview in WinApps"))
                .IsVisible(!terminate && Match(item) is not null && (Props.OnSelect is not null || Props.OnSelectActivity is not null)),
            VStack(0).Flex(grow: 1, basis: 0),
            GlassButton(Button(FluentGlyph("\uE8A7"),
                () => message.Set(ApplicationWindowSwitcher.SwitchTo(item))), true)
                .AutomationName($"Switch to {item.Name}").Width(30).Height(30).Padding(0).CornerRadius(7)
                .HAlign(HorizontalAlignment.Right)
                .Set(control => Microsoft.UI.Xaml.Controls.ToolTipService.SetToolTip(control, $"Switch to {item.Name}"))
                .IsVisible(item.WindowHandle != 0)),
            terminate ? Disclosure("Executable location", ReadOnlyText(item.Path.Length > 0 ? item.Path : "Access unavailable", "Executable location")) : Text("").IsVisible(false),
            ActionButton("Close application", () => pending.Set(item)).IsVisible(terminate)
                .IsEnabled(item.Id != Environment.ProcessId && item.Path.Length > 0)
        ).Padding(14)).Width(304)
            .WithBorder(Theme.Ref("ControlStrokeColorDefaultBrush"), _newApps.Contains((item.Id, item.Started)) ? 2 : 1)
            .Set(control =>
            {
                var canOpen = !terminate && Match(item) is not null && (Props.OnSelect is not null || Props.OnSelectActivity is not null);
                AttachOpen(control, canOpen ? () => OpenOverview(item) : null);
                if (canOpen) BrowserMotion.CardSelectionFeedback(control, false);
                BrowserMotion.NewActivityBorder(control, _newApps.Contains((item.Id, item.Started)));
            })
            .AutomationName($"{item.Name}, {item.Memory / 1048576d:N1} MiB RAM, PID {item.Id}")
            .WithKey($"process-{item.Id}-{item.Started}");

        return VStack(12,
            Props.App is null ? Text("").IsVisible(false)
                : ActivityCardStyle.Section($"{Props.App.Name} · live",
                HStack(8, TextBlock("●").Foreground(Theme.Ref(loading.Value ? "SystemFillColorCautionBrush" : items.Length > 0 ? "SystemFillColorSuccessBrush" : "SystemFillColorCriticalBrush")),
                    Text(loading.Value ? "Checking activity…" : items.Length > 0 ? "Running" : "No accessible processes detected")),
                Text(loading.Value ? "Reading application activity…" : $"{items.Length} {(Props.App is null ? "windowed apps" : "matching processes")}  ·  {items.Sum(item => item.Memory) / 1048576d:N0} MiB RAM  ·  Updates every 5 seconds"),
                Props.App is not null ? FlexRow(
                    Metric("RAM", $"{items.Sum(item => item.Memory) / 1048576d:N1} MiB"),
                    Metric("CPU", loading.Value || items.Any(item => item.CpuPercent is null) ? "Sampling…" : $"{items.Sum(item => item.CpuPercent ?? 0):N1}%"),
                    Metric("Processes", items.Length.ToString("N0")), Metric("Threads", items.Sum(item => item.Threads).ToString("N0"))) : Text("").IsVisible(false),
                Text("").IsVisible(false)),
            Props.App is null && items.Length > 0 ? ActivityCardStyle.Section("On your PC now",
                FlexRow(Text("Swipe or scroll to explore running apps").Flex(grow: 1, basis: 0),
                    HStack(8,
                        ActionButton("Refresh activity", () => { loading.Set(true); refreshVersion.Set(refreshVersion.Value + 1); }).IsEnabled(!loading.Value),
                        GlassButton(Button(FluentGlyph("\uE76B"), () => MoveCarousel(-1))).AutomationName("Previous application cards").Width(36).Height(36).CornerRadius(8),
                        GlassButton(Button(FluentGlyph("\uE76C"), () => MoveCarousel(1))).AutomationName("Next application cards").Width(36).Height(36).CornerRadius(8))),
                ScrollView(HStack(16, items.Select(item => ProcessCard(item, false)
                    .WithKey($"carousel-{item.Id}-{item.Started}")).ToArray())
                    .Padding(left: 8, top: 8, right: 8, bottom: 20))
                    .Set(control =>
                    {
                        _carousel = control;
                        AttachAcknowledgement(control, AcknowledgeNewApps);
                        if (_revealNewCards)
                            control.DispatcherQueue.TryEnqueue(() =>
                            {
                                if (!_revealNewCards) return;
                                control.UpdateLayout();
                                control.ScrollTo(0, 0, new Microsoft.UI.Xaml.Controls.ScrollingScrollOptions(
                                    Microsoft.UI.Xaml.Controls.ScrollingAnimationMode.Disabled, Microsoft.UI.Xaml.Controls.ScrollingSnapPointsMode.Ignore));
                                _revealNewCards = false;
                            });
                        BrowserMotion.HorizontalMouseWheel(control);
                        control.ContentOrientation = Microsoft.UI.Xaml.Controls.ScrollingContentOrientation.Horizontal;
                        control.HorizontalScrollMode = Microsoft.UI.Xaml.Controls.ScrollingScrollMode.Enabled;
                        control.HorizontalScrollBarVisibility = Microsoft.UI.Xaml.Controls.ScrollingScrollBarVisibility.Auto;
                        control.VerticalScrollMode = Microsoft.UI.Xaml.Controls.ScrollingScrollMode.Disabled;
                        control.VerticalScrollBarVisibility = Microsoft.UI.Xaml.Controls.ScrollingScrollBarVisibility.Hidden;
                    }).MinWidth(0).AutomationName("Running application carousel"))
                : Text("").IsVisible(false),
            Text(!loading.Value && items.Length == 0 ? "No accessible running applications found." : "").IsVisible(!loading.Value && items.Length == 0),
            Props.App is null ? Component<StartupApplicationsPanel>().WithKey("startup-apps") : Text("").IsVisible(false),
            Text(message.Value).IsVisible(message.Value.Length > 0),
            (ContentDialog($"Close {pending.Value?.Name}?",
                Text($"Request closing the selected window for PID {pending.Value?.Id}. The application can prompt to save work."), "Close application") with
            {
                IsOpen = pending.Value is not null,
                CloseButtonText = "Cancel",
                DefaultButton = Microsoft.UI.Xaml.Controls.ContentDialogButton.Close,
                OnClosed = result =>
                {
                    var target = pending.Value;
                    pending.Set(null);
                    if (result == Microsoft.UI.Xaml.Controls.ContentDialogResult.Primary && target is not null)
                        message.Set(RunningApplications.RequestClose(target));
                }
            }).WithKey("terminate-confirmation"));
    }

    private static Element Metric(string label, string value) => ActivityCardStyle.Surface(VStack(8,
        ActivityCardStyle.AccentLine(),
        TextBlock(value).FontSize(24).SemiBold().TextWrapping().Foreground(Theme.Ref("AccentTextFillColorPrimaryBrush"))
            .Set(control => control.TextAlignment = TextAlignment.Center),
        TextBlock(label).Foreground(Theme.SecondaryText).Set(control => control.TextAlignment = TextAlignment.Center)).Padding(14))
        .Margin(right: 8).MinWidth(0).Flex(grow: 1, basis: 0);
}
