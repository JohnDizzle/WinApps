// SPDX-License-Identifier: GPL-3.0-only
using Microsoft.UI.Xaml;
using Microsoft.UI.Reactor;
using Microsoft.UI.Reactor.Core;
using WinApps.Models;
using WinApps.Services.Inventory;
using static Microsoft.UI.Reactor.Factories;
using static WinApps.Browser.BrowserUi;

namespace WinApps.Browser;

internal sealed record ApplicationResourceCardProps(InstalledApp App, RunningApplication[]? InitialSnapshot = null);
internal sealed class ApplicationResourceCard : Component<ApplicationResourceCardProps>
{
    private readonly Dictionary<string, Microsoft.UI.Xaml.Controls.TextBlock> _labels = new();
    private RunningApplication[] _processes = [];
    private bool _loading = true;
    private bool _failed;
    private bool _initialized;
    private void UpdateLabels()
    {
        void Set(string name, string value) { if (_labels.TryGetValue(name, out var label)) label.Text = value; }
        Set("state", _loading ? "Checking activity" : _failed ? "Activity unavailable" : _processes.Length > 0 ? "Running" : "No processes detected");
        Set("RAM", _loading || _failed ? "—" : $"{_processes.Sum(item => item.Memory) / 1048576d:N1} MiB");
        Set("CPU", _loading || _failed ? "—" : _processes.Any(item => item.CpuPercent is null) ? "Sampling…" : $"{_processes.Sum(item => item.CpuPercent ?? 0):N1}%");
        Set("Processes", _processes.Length.ToString()); Set("Threads", _processes.Sum(item => item.Threads).ToString());
        if (_labels.TryGetValue("light", out var light))
        {
            var color = _loading || _failed ? Windows.UI.Color.FromArgb(255, 255, 185, 0)
                : _processes.Length > 0 ? Windows.UI.Color.FromArgb(255, 108, 203, 95) : Windows.UI.Color.FromArgb(255, 255, 100, 100);
            light.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(color);
        }
    }
    public override Element Render()
    {
        if (!_initialized)
        {
            _initialized = true;
            if (Props.InitialSnapshot is { Length: > 0 } snapshot) { _processes = snapshot; _loading = false; }
        }
        UseEffect(() =>
        {
            var dispatcher = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();
            var cancellation = new CancellationTokenSource();
            var token = cancellation.Token;
            var disposed = false;
            var reading = false;
            var sampler = new ProcessCpuSampler();
            var clock = System.Diagnostics.Stopwatch.StartNew();
            async void Refresh()
            {
                if (reading) return;
                reading = true;
                try
                {
                    var found = await Task.Run(() => RunningApplications.Read(Props.App, false, token));
                    if (token.IsCancellationRequested) return;
                    var cpu = sampler.Sample(found.Select(item => (item.Id, item.Started, item.CpuTicks)).ToArray(), clock.Elapsed, Environment.ProcessorCount);
                    dispatcher.TryEnqueue(() =>
                    {
                        if (disposed) return;
                        _processes = found.Select((item, index) => item with { CpuPercent = cpu[index] }).ToArray();
                        _loading = false; _failed = false; UpdateLabels();
                    });
                }
                catch { dispatcher.TryEnqueue(() => { if (!disposed) { _failed = true; _loading = false; UpdateLabels(); } }); }
                finally { reading = false; }
            }
            var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
            void Tick(object? sender, object args) => Refresh();
            timer.Tick += Tick; timer.Start(); Refresh();
            return () => { disposed = true; timer.Stop(); timer.Tick -= Tick; cancellation.Cancel(); cancellation.Dispose(); };
        }, Props.App.FullName);
        var items = _processes;
        var state = _loading ? "Checking activity" : _failed ? "Activity unavailable" : items.Length > 0 ? "Running" : "No processes detected";
        Element Metric(string value, string label) => ActivityCardStyle.Surface(VStack(4,
            TextBlock(value).FontSize(22).SemiBold().TextWrapping().Set(control => { control.TextAlignment = TextAlignment.Center; _labels[label] = control; }),
            TextBlock(label).Foreground(Theme.SecondaryText).Set(control => control.TextAlignment = TextAlignment.Center)).Padding(12))
            .Margin(right: 8).MinWidth(0).Flex(grow: 1, basis: 0);
        return ActivityCardStyle.Section("Resource footprint · live",
            HStack(10, AppIcon(Props.App, 24), TextBlock(Props.App.Name).FontSize(16).SemiBold().TextWrapping(),
            HStack(6, TextBlock("●").Set(control => _labels["light"] = control),
                TextBlock(state).FontSize(12).Set(control => _labels["state"] = control))),
            FlexRow(
                Metric(_loading || _failed ? "—" : $"{items.Sum(item => item.Memory) / 1048576d:N1} MiB", "RAM"),
                Metric(_loading || _failed ? "—" : items.Any(item => item.CpuPercent is null) ? "Sampling…" : $"{items.Sum(item => item.CpuPercent ?? 0):N1}%", "CPU"),
                Metric(items.Length.ToString(), "Processes"), Metric(items.Sum(item => item.Threads).ToString(), "Threads")))
            .Set(control =>
            {
                UpdateLabels();
                Microsoft.UI.Xaml.Controls.ToolTipService.SetToolTip(control,
                    "Updates every 5 seconds. RAM is working set; CPU is a percentage of total processor capacity. Executables match the install folder; shared folders may include other apps.");
            });
    }
}
