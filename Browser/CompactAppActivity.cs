// SPDX-License-Identifier: GPL-3.0-only
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using WinApps.Models;
using WinApps.Services.Inventory;

namespace WinApps.Browser;

internal sealed class CompactActivityTile(InstalledApp app, Button button, Border badge, TextBlock count,
    TextBlock status, Border indicator)
{
    internal InstalledApp App => app;
    private bool _stopped;

    internal void Update(RunningApplication[] instances)
    {
        if (_stopped) return;
        var processes = instances.DistinctBy(item => item.Id).ToArray();
        var total = processes.Length;
        var memory = processes.Sum(item => item.Memory) / 1048576d;
        status.Text = total > 0 ? $"Running · {memory:N0} MiB RAM" : "Suggested";
        count.Text = $"{total} processes";
        badge.Visibility = total > 1 ? Visibility.Visible : Visibility.Collapsed;
        indicator.Background = new SolidColorBrush(total > 0
            ? Windows.UI.Color.FromArgb(255, 52, 199, 89)
            : Windows.UI.Color.FromArgb(255, 142, 142, 147));
        var details = total == 0 ? "Not running" :
            $"Running · {memory:N1} MiB RAM in {total} windowed {(total == 1 ? "process" : "processes")}\n" +
            "Working set snapshot; background helper processes are not included.\n" +
            string.Join("\n", processes.Select(item => $"{(string.IsNullOrWhiteSpace(item.Title) ? item.Name : item.Title)} · PID {item.Id}"));
        ToolTipService.SetToolTip(button, $"Open {app.Name} overview\n{details}");
        ToolTipService.SetToolTip(badge, details);
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetItemStatus(button, details);
    }

    internal void Stop() => _stopped = true;
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
