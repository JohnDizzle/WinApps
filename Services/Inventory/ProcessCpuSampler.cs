// SPDX-License-Identifier: GPL-3.0-only
namespace WinApps.Services.Inventory;

internal sealed class ProcessCpuSampler
{
    private Dictionary<(int Id, long Started), long> _previous = new();
    private TimeSpan? _sampleTime;

    internal double?[] Sample((int Id, long Started, long CpuTicks)[] processes, TimeSpan now, int processors)
    {
        var elapsed = _sampleTime is { } previousTime ? (now - previousTime).Ticks : 0;
        var result = processes.Select(item => elapsed > 0 && processors > 0 &&
            _previous.TryGetValue((item.Id, item.Started), out var previous) && item.CpuTicks >= previous
                ? (double?)Math.Clamp((item.CpuTicks - previous) * 100d / elapsed / processors, 0, 100) : null).ToArray();
        _previous = processes.ToDictionary(item => (item.Id, item.Started), item => item.CpuTicks);
        _sampleTime = now;
        return result;
    }
}
