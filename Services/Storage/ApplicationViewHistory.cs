// SPDX-License-Identifier: GPL-3.0-only
using System.Text.Json;
using WinApps.Models;

namespace WinApps.Services.Storage;

public sealed record SuggestionPolicy(bool Enabled = false, int RetentionDays = 30, double HalfLifeDays = 7, int DailyViewLimit = 20);
public sealed record SuggestionMetrics(long Shown, long Hits)
{
    public double? HitRate => Shown == 0 ? null : (double)Hits / Shown;
}

public sealed class ApplicationViewHistory
{
    public static ApplicationViewHistory Shared { get; } = new();
    public sealed record DailyApp(DateOnly Day, string Key, int Views = 0, int Impressions = 0, int Hits = 0);
    public sealed record DailyOutcome(DateOnly Day, int Shown = 0, int Hits = 0);
    public sealed record Snapshot(int Version, SuggestionPolicy Policy, DailyApp[] Apps, DailyOutcome[] Outcomes);
    private sealed record Exposure(DateOnly Day, HashSet<string> Keys);
    private readonly string _path;
    private readonly Func<DateOnly> _today;
    private readonly List<DailyApp> _apps = [];
    private readonly List<DailyOutcome> _outcomes = [];
    private readonly Dictionary<string, Exposure> _pending = new(StringComparer.Ordinal);
    public SuggestionPolicy Policy { get; private set; } = new();
    public event Action? Changed;


    public ApplicationViewHistory(string? path = null, Func<DateOnly>? today = null)
    {
        _path = path ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "WinApps", "app-views.json");
        _today = today ?? (() => DateOnly.FromDateTime(DateTime.Now));
        try
        {
            using var stream = File.OpenRead(_path);
            if (stream.Length > 4 * 1024 * 1024) return;
            var saved = JsonSerializer.Deserialize<Snapshot>(stream);
            if (saved is not { Version: 2, Policy: not null, Apps: not null, Outcomes: not null }) return;
            Policy = Normalize(saved.Policy);
            _apps.AddRange(saved.Apps.Where(item => item is not null && !string.IsNullOrWhiteSpace(item.Key)
                && item.Views >= 0 && item.Impressions >= 0 && item.Hits >= 0 && item.Hits <= item.Impressions)
                .DistinctBy(item => (item.Day, item.Key)).Take(8192));
            _outcomes.AddRange(saved.Outcomes.Where(item => item is not null && item.Shown >= 0 && item.Hits >= 0 && item.Hits <= item.Shown)
                .DistinctBy(item => item.Day).Take(90));
            Prune();
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException) { }
    }

    private static SuggestionPolicy Normalize(SuggestionPolicy policy) => policy with
    {
        RetentionDays = Math.Clamp(policy.RetentionDays, 1, 90),
        HalfLifeDays = double.IsFinite(policy.HalfLifeDays) ? Math.Clamp(policy.HalfLifeDays, 1, 30) : 7,
        DailyViewLimit = Math.Clamp(policy.DailyViewLimit, 1, 100)
    };
    private static string Key(InstalledApp app) => app.Source == "MSIX" && !string.IsNullOrWhiteSpace(app.FamilyName)
        ? "package:" + app.FamilyName : "app:" + app.FullName;
    private bool Retained(DateOnly day) => day <= _today() && day.DayNumber > _today().DayNumber - Policy.RetentionDays;
    private void Prune()
    {
        _apps.RemoveAll(item => !Retained(item.Day));
        _outcomes.RemoveAll(item => !Retained(item.Day));
        if (_apps.Count > 8192)
        {
            _apps.Sort((left, right) => right.Day.CompareTo(left.Day));
            _apps.RemoveRange(8192, _apps.Count - 8192);
        }
        foreach (var source in _pending.Where(item => !Retained(item.Value.Day)).Select(item => item.Key).ToArray()) _pending.Remove(source);
    }
    public void SetPolicy(SuggestionPolicy policy)
    {
        Policy = Normalize(policy);
        _pending.Clear();
        Prune();
        Save();
    }
    public void Clear()
    {
        _apps.Clear(); _outcomes.Clear(); _pending.Clear();
        Save();
    }
    public SuggestionMetrics Metrics()
    {
        Prune();
        return new(_outcomes.Sum(item => (long)item.Shown), _outcomes.Sum(item => (long)item.Hits));
    }
    public InstalledApp[] MostViewed(IEnumerable<InstalledApp> apps, int limit = 6)
    {
        Prune();
        if (!Policy.Enabled) return [];
        var scores = _apps.Where(item => item.Views > 0).GroupBy(item => item.Key).ToDictionary(group => group.Key,
            group => group.Sum(item => (Math.Min(item.Views, Policy.DailyViewLimit) + 1 + Math.Min(item.Hits, Policy.DailyViewLimit) * 0.25)
                * Math.Pow(0.5, (_today().DayNumber - item.Day.DayNumber) / Policy.HalfLifeDays)), StringComparer.Ordinal);
        return apps.Where(app => scores.ContainsKey(Key(app))).DistinctBy(Key)
            .OrderByDescending(app => scores[Key(app)]).ThenBy(app => app.Name, StringComparer.OrdinalIgnoreCase)
            .Take(Math.Clamp(limit, 0, 100)).ToArray();
    }
    private void UpdateApp(DateOnly day, string key, Func<DailyApp, DailyApp> update)
    {
        var index = _apps.FindIndex(item => item.Day == day && item.Key == key);
        if (index >= 0) _apps[index] = update(_apps[index]);
        else _apps.Add(update(new(day, key)));
    }
    public void Record(InstalledApp app)
    {
        if (!Policy.Enabled) return;
        UpdateApp(_today(), Key(app), item => item with { Views = Math.Min(Policy.DailyViewLimit, Math.Min(item.Views, Policy.DailyViewLimit) + 1) });
        Save();
    }
    public void RecordExposure(IEnumerable<InstalledApp> apps, string surface)
    {
        if (!Policy.Enabled) return;
        var keys = apps.Select(Key).ToHashSet(StringComparer.Ordinal);
        if (keys.Count == 0) { _pending.Remove(surface); return; }
        var day = _today();
        _pending[surface] = new(day, keys);
        foreach (var key in keys) UpdateApp(day, key, item => item with { Impressions = Math.Min(int.MaxValue - 1, item.Impressions) + 1 });
        var index = _outcomes.FindIndex(item => item.Day == day);
        if (index < 0) _outcomes.Add(new(day, 1));
        else _outcomes[index] = _outcomes[index] with { Shown = Math.Min(int.MaxValue - 1, _outcomes[index].Shown) + 1 };
        Save();
    }
    public void RecordHit(InstalledApp app, string surface)
    {
        if (!Policy.Enabled || !_pending.TryGetValue(surface, out var exposure) || !Retained(exposure.Day) || !exposure.Keys.Contains(Key(app))) return;
        _pending.Remove(surface); // At most one hit per displayed suggestion set.
        UpdateApp(exposure.Day, Key(app), item => item with { Hits = Math.Min(item.Impressions, Math.Min(int.MaxValue - 1, item.Hits) + 1) });
        var index = _outcomes.FindIndex(item => item.Day == exposure.Day);
        if (index >= 0) _outcomes[index] = _outcomes[index] with { Hits = Math.Min(_outcomes[index].Shown, Math.Min(int.MaxValue - 1, _outcomes[index].Hits) + 1) };
        Save();
    }
    private void Save()
    {
        Prune();
        var temporary = _path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(_path))!);
            File.WriteAllText(temporary, JsonSerializer.Serialize(new Snapshot(2, Policy,
                _apps.OrderByDescending(item => item.Day).Take(8192).ToArray(), _outcomes.ToArray())));
            File.Move(temporary, _path, overwrite: true);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException) { }
        finally
        {
            try { File.Delete(temporary); }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException) { }
        }
        Changed?.Invoke();
    }
}
