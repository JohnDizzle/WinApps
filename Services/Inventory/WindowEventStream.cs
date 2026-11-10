// SPDX-License-Identifier: GPL-3.0-only
using System.ComponentModel;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Microsoft.UI.Dispatching;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Accessibility;

namespace WinApps.Services.Inventory;

internal sealed record WindowEventEntry(DateTimeOffset Time, string Name, nint Window, uint Thread, int ObjectId, int ChildId, string Title = "Title unavailable")
{
    public override string ToString() => $"{Time:HH:mm:ss.fff}  {Name,-20}  {Title}  HWND 0x{Window:X}  TID {Thread}  object {ObjectId}  child {ChildId}";
}

// Hooks live on a dedicated message-loop thread. Callbacks only enqueue records;
// bounded metadata processing and UI rendering happen outside the callback.
internal sealed unsafe class WindowEventStream : IDisposable
{
    internal const int Capacity = 2000;
    private readonly Queue<WindowEventEntry> _pending = new();
    private readonly object _gate = new();
    private readonly record struct Notification(nint Hook, uint Kind, nint Window, int ObjectId, int ChildId, uint Thread, DateTimeOffset Time, int Generation);
    private readonly Queue<Notification> _raw = new();
    private readonly Dictionary<nint, (uint Thread, string Title, long ReadAt)> _titles = [];
    private readonly Dictionary<nint, bool> _hookProcesses = [];
    private readonly DispatcherQueueController _controller;
    private DispatcherQueueTimer? _timer;
    private readonly TaskCompletionSource _ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource _stopped = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly List<UnhookWinEventSafeHandle> _hooks = [];
    private static readonly ConcurrentDictionary<nint, WindowEventStream> HookOwners = new();
    private readonly int _processId;
    private readonly nint _frame;
    private readonly nint _excludedWindow;
    private readonly uint _frameThread;
    private uint _captureThread;
    private volatile bool _disposed;
    private volatile bool _paused;
    private volatile bool _includeControlObjects;
    private volatile string? _error;
    private int _generation;
    private long _dropped;
    internal Task Ready => _ready.Task;
    internal Task Stopped => _stopped.Task;
    internal string? Error => _error;
    internal bool Paused
    {
        get => _paused;
        set { lock (_gate) { _paused = value; _generation++; _raw.Clear(); } }
    }
    internal bool IncludeControlObjects
    {
        get => _includeControlObjects;
        set { lock (_gate) { _includeControlObjects = value; _generation++; _raw.Clear(); } }
    }
    internal long Dropped => Interlocked.Read(ref _dropped);
    internal static readonly IReadOnlyDictionary<uint, string> Names = new Dictionary<uint, string>
    {
        [3] = "Foreground", [8] = "Capture started", [9] = "Capture ended",
        [10] = "Move/resize started", [11] = "Move/resize ended", [22] = "Minimized", [23] = "Restored",
        [0x8000] = "Created", [0x8001] = "Destroyed", [0x8002] = "Shown", [0x8003] = "Hidden",
        [0x8004] = "Reordered", [0x8005] = "Focus", [0x800A] = "State changed",
        [0x800B] = "Location changed", [0x800C] = "Name changed", [0x800F] = "Parent changed",
        [0x8013] = "Invoked"
    };

    internal WindowEventStream(int processId, nint representative, nint excludedWindow = 0)
    {
        _processId = processId;
        _frame = representative;
        _excludedWindow = excludedWindow;
        _frameThread = WindowInspection.GetWindowThreadProcessId(representative, out var owner);
        _controller = DispatcherQueueController.CreateOnDedicatedThread();
        _controller.DispatcherQueue.TryEnqueue(() =>
        {
            try
            {
                if (_disposed) return;
                _captureThread = PInvoke.GetCurrentThreadId();
                AddHooks((uint)processId);
                if (owner != 0 && owner != processId) AddHooks(owner);
                _timer = _controller.DispatcherQueue.CreateTimer();
                _timer.Interval = TimeSpan.FromMilliseconds(50);
                _timer.Tick += ProcessNotifications;
                _timer.Start();
            }
            catch (Exception error) { _error = error.Message; ReleaseHooks(); }
            finally { _ready.TrySetResult(); }
        });
    }

    private void AddHooks(uint process)
    {
        foreach (var range in new[] { (3u, 3u), (8u, 11u), (22u, 23u), (0x8000u, 0x8005u), (0x800Au, 0x800Cu), (0x800Fu, 0x800Fu), (0x8013u, 0x8013u) })
        {
            var hook = PInvoke.SetWinEventHook(range.Item1, range.Item2, default(System.Runtime.InteropServices.SafeHandle), &DispatchEvent, process, 0, PInvoke.WINEVENT_OUTOFCONTEXT);
            if (hook.IsInvalid)
            {
                var error = Marshal.GetLastWin32Error();
                hook.Dispose();
                throw new Win32Exception(error, "Windows could not start window event monitoring.");
            }
            _hooks.Add(hook);
            HookOwners[hook.DangerousGetHandle()] = this;
            _hookProcesses[hook.DangerousGetHandle()] = process == _processId;
        }
    }

    private void OnEvent(HWINEVENTHOOK hook, uint kind, HWND nativeWindow, int objectId, int childId, uint thread, uint time)
    {
        try
        {
            if (_disposed || _paused || nativeWindow == default || thread == _captureThread || (nint)nativeWindow.Value == _excludedWindow || !Names.ContainsKey(kind)) return;
            if (!_includeControlObjects && kind >= 0x8000 && (objectId != 0 || childId != 0) && !(kind == 0x8005 && objectId == -4 && childId == 0)) return;
            lock (_gate)
            {
                if (_disposed || _paused) return;
                if (_raw.Count == Capacity) { _raw.Dequeue(); Interlocked.Increment(ref _dropped); }
                _raw.Enqueue(new((nint)hook.Value, kind, (nint)nativeWindow.Value, objectId, childId, thread, DateTimeOffset.Now, _generation));
            }
        }
        catch (Exception) { Interlocked.Increment(ref _dropped); }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static void DispatchEvent(HWINEVENTHOOK hook, uint kind, HWND window, int objectId, int childId, uint thread, uint time)
    {
        if (HookOwners.TryGetValue((nint)hook.Value, out var owner)) owner.OnEvent(hook, kind, window, objectId, childId, thread, time);
    }

    private void ProcessNotifications(DispatcherQueueTimer sender, object args)
    {
        var budget = Stopwatch.StartNew();
        for (var count = 0; count < 100 && budget.ElapsedMilliseconds < 8 && !_disposed; count++)
        {
            Notification item;
            lock (_gate) { if (_paused || !_raw.TryDequeue(out item)) return; }
            ProcessEvent(item);
        }
    }

    private void ProcessEvent(Notification item)
    {
        try
        {
            var (hook, kind, window, objectId, childId, thread, time, generation) = item;
            if (_disposed || _paused || !Names.TryGetValue(kind, out var name)) return;
            if (_excludedWindow != 0 && (window == _excludedWindow || WindowInspection.GetAncestor(window) == _excludedWindow)) return;
            WindowInspection.GetWindowThreadProcessId(window, out var owner);
            // Destroy notifications can arrive after the HWND is invalid. Hook scope
            // still identifies the target process; shared UWP frame hosts need filtering.
            if (owner != _processId && !(window == _frame && thread == _frameThread) &&
                !(owner == 0 && kind == 0x8001 && _hookProcesses.GetValueOrDefault(hook))) return;
            var title = SnapshotTitle(window, thread, owner == 0, kind == 0x800C && objectId == 0 && childId == 0);
            lock (_gate)
            {
                if (_disposed || _paused || generation != _generation) return;
                if (_pending.Count == Capacity) { _pending.Dequeue(); Interlocked.Increment(ref _dropped); }
                _pending.Enqueue(new(time, name, window, thread, objectId, childId, title));
            }
        }
        catch (Exception) { Interlocked.Increment(ref _dropped); }
    }

    private string SnapshotTitle(nint window, uint thread, bool destroyed, bool captionChanged)
    {
        if (destroyed)
        {
            if (_titles.Remove(window, out var previous) && previous.Thread == thread) return previous.Title;
            return "Closed window · title unavailable";
        }
        var now = Environment.TickCount64;
        if (!captionChanged && _titles.TryGetValue(window, out var cached) && cached.Thread == thread && now - cached.ReadAt < 500) return cached.Title;
        var details = WindowInspection.Read(window);
        var title = details.Title;
        if (string.IsNullOrWhiteSpace(title))
        {
            var root = WindowInspection.GetAncestor(window);
            var parentTitle = root != 0 && root != window ? WindowInspection.Read(root).Title : "";
            title = string.IsNullOrWhiteSpace(parentTitle) ? $"Untitled {details.Class}" : $"{parentTitle} · child {details.Class}";
        }
        // Cache only bounded, last-seen context for destroy notifications.
        if (_titles.Count >= Capacity && !_titles.ContainsKey(window)) _titles.Remove(_titles.Keys.First());
        title = title.Replace('\r', ' ').Replace('\n', ' ').Replace('\t', ' ');
        _titles[window] = (thread, title, now);
        return title;
    }

    internal WindowEventEntry[] Drain(int maximum = Capacity)
    {
        lock (_gate)
        {
            var entries = new WindowEventEntry[Math.Min(Math.Max(maximum, 0), _pending.Count)];
            for (var i = 0; i < entries.Length; i++) entries[i] = _pending.Dequeue();
            return entries;
        }
    }

    internal void Clear() { lock (_gate) { _generation++; _raw.Clear(); _pending.Clear(); Interlocked.Exchange(ref _dropped, 0); } }

    private void ReleaseHooks()
    {
        if (_timer is not null) { _timer.Stop(); _timer.Tick -= ProcessNotifications; _timer = null; }
        foreach (var hook in _hooks) { HookOwners.TryRemove(hook.DangerousGetHandle(), out _); hook.Dispose(); }
        _hooks.Clear();
        _hookProcesses.Clear();
        _titles.Clear();
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed) return;
            _disposed = true; _generation++; _raw.Clear(); _pending.Clear();
        }
        // Unhook on the registering thread without waiting on the UI thread.
        _controller.DispatcherQueue.TryEnqueue(() =>
        {
            ReleaseHooks();
            _controller.ShutdownQueueAsync().AsTask().ContinueWith(task =>
            {
                if (task.IsFaulted) _error = task.Exception?.GetBaseException().Message;
                _stopped.TrySetResult();
            }, TaskScheduler.Default);
        });
    }
}
