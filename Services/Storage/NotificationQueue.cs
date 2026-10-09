// SPDX-License-Identifier: GPL-3.0-only
namespace WinApps.Services.Storage;

public sealed record NotificationEntry(long Id, string Message, DateTimeOffset ExpiresAt);

public sealed class NotificationQueue
{
    private long _nextId;
    public IReadOnlyList<NotificationEntry> Entries { get; private set; } = [];
    public event Action? Changed;

    public void Add(string message, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(message)) return;
        Entries = Entries.Append(new(++_nextId, message, now.AddSeconds(5))).TakeLast(4).ToArray();
        Changed?.Invoke();
    }

    public void Expire(DateTimeOffset now)
    {
        var remaining = Entries.Where(entry => entry.ExpiresAt > now).ToArray();
        if (remaining.Length == Entries.Count) return;
        Entries = remaining;
        Changed?.Invoke();
    }

    public void Dismiss(long id)
    {
        Entries = Entries.Where(entry => entry.Id != id).ToArray();
        Changed?.Invoke();
    }
}
