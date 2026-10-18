// SPDX-License-Identifier: GPL-3.0-only
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using WinApps.Models;
using WinApps.Services.Inventory;

namespace WinApps.Browser;

internal sealed class CompactActivityTile(InstalledApp app, Button button, Border badge, TextBlock count)
{
    internal InstalledApp App => app;
    private int _count = -1;
    private DispatcherTimer? _pulse;
    private Storyboard? _animation;
    private bool _stopped;

    internal void Update(RunningApplication[] instances)
    {
        if (_stopped) return;
        var total = instances.Length;
        var details = total == 0 ? "Not running" : $"{total} running {(total == 1 ? "instance" : "instances")}\n" +
            string.Join("\n", instances.Select(item => $"{(string.IsNullOrWhiteSpace(item.Title) ? item.Name : item.Title)} · PID {item.Id}"));
        ToolTipService.SetToolTip(button, $"Open {app.Name} overview\n{details}");
        ToolTipService.SetToolTip(badge, details);
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetItemStatus(button, details);
        if (total == _count) return;
        var previous = _count;
        _count = total;
        count.Text = total.ToString();
        _pulse?.Stop();
        _animation?.Stop();
        _animation = null;
        badge.ClearValue(Border.BorderBrushProperty);
        button.ClearValue(Control.BorderBrushProperty);
        badge.Visibility = total > 0 ? Visibility.Visible : Visibility.Collapsed;
        var changed = total > 0 && previous <= 0 || total == 0 && previous > 0;
        if (new Windows.UI.ViewManagement.AccessibilitySettings().HighContrast || !changed && total == 0) return;
        badge.Visibility = Visibility.Visible;
        var color = total > 0 ? Windows.UI.Color.FromArgb(255, 42, 150, 83) : Windows.UI.Color.FromArgb(255, 194, 64, 64);
        var brush = new SolidColorBrush(color);
        button.BorderBrush = brush;
        badge.BorderBrush = brush;
        if (!changed) return;
        if (new Windows.UI.ViewManagement.UISettings().AnimationsEnabled)
        {
            var fade = new DoubleAnimation
            {
                From = 0.25, To = 1, AutoReverse = true, RepeatBehavior = new RepeatBehavior(2),
                Duration = TimeSpan.FromMilliseconds(400), EnableDependentAnimation = true
            };
            Storyboard.SetTarget(fade, brush);
            Storyboard.SetTargetProperty(fade, "Opacity");
            var storyboard = new Storyboard();
            storyboard.Children.Add(fade);
            storyboard.Begin();
            _animation = storyboard;
        }
        _pulse = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1700) };
        _pulse.Tick += (_, _) =>
        {
            if (_stopped) return;
            _pulse?.Stop();
            _animation?.Stop();
            brush.Opacity = 1;
            if (_count == 0)
            {
                button.ClearValue(Control.BorderBrushProperty);
                badge.ClearValue(Border.BorderBrushProperty);
            }
            badge.Visibility = _count > 0 ? Visibility.Visible : Visibility.Collapsed;
        };
        _pulse.Start();
    }

    internal void Stop()
    {
        _stopped = true;
        _pulse?.Stop(); _pulse = null;
        _animation?.Stop(); _animation = null;
    }
}

internal sealed class CompactAppActivity(Window window, Func<InstalledApp[]> apps, Func<IReadOnlyList<CompactActivityTile>> tiles,
    Action<IReadOnlySet<string>>? activeChanged = null) : IDisposable
{
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(2) };
    private readonly CancellationTokenSource _cancellation = new();
    private bool _busy;
    private bool _disposed;

    internal void Start()
    {
        _timer.Tick += Tick;
        _timer.Start();
    }
    private void Tick(object? sender, object args) => Refresh();

    internal async void Refresh()
    {
        if (_busy || _disposed || !window.AppWindow.IsVisible) return;
        _busy = true;
        try
        {
            var token = _cancellation.Token;
            var running = await Task.Run(() => RunningApplications.Read(null, includePresentation: false, cancellationToken: token));
            if (_disposed || token.IsCancellationRequested || !window.AppWindow.IsVisible) return;
            var library = apps();
            var groups = running.Where(item => item.WindowHandle != 0)
                .Select(item => (App: RunningApplicationMatch.Find(item.Path, item.Name, library), Item: item))
                .Where(pair => pair.App is not null)
                .GroupBy(pair => pair.App!.FullName, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.Select(pair => pair.Item).DistinctBy(item => item.Id).ToArray(), StringComparer.Ordinal);
            activeChanged?.Invoke(groups.Keys.ToHashSet(StringComparer.Ordinal));
            foreach (var tile in tiles()) tile.Update(groups.GetValueOrDefault(tile.App.FullName) ?? []);
        }
        catch (Exception error)
        {
            // Keep the last known state on inaccessible/transient process failures.
            System.Diagnostics.Debug.WriteLine($"Compact activity refresh unavailable: {error.Message}");
        }
        finally { _busy = false; }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _timer.Stop();
        _timer.Tick -= Tick;
        _cancellation.Cancel();
        foreach (var tile in tiles()) tile.Stop();
        _cancellation.Dispose();
    }
}
