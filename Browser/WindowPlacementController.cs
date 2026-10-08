// SPDX-License-Identifier: GPL-3.0-only
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using MyApps.Services.Storage;
using Windows.Graphics;

namespace MyApps.Browser;

internal sealed class WindowPlacementController
{
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Window, WindowPlacementController> Controllers = new();
    private readonly Window _window;
    private readonly WindowPlacementStore _store = new();
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(350) };
    private WindowPlacement _normal;
    private bool _maximized;
    private bool _adjusting;

    internal static void Attach(Window window)
    {
        if (Controllers.TryGetValue(window, out _)) return;
        Controllers.Add(window, new WindowPlacementController(window));
    }

    private WindowPlacementController(Window window)
    {
        _window = window;
        var app = window.AppWindow;
        var saved = _store.Load() ?? new WindowPlacement(Math.Max(0, app.Position.X), Math.Max(0, app.Position.Y),
            WindowPlacementStore.DefaultWidth, WindowPlacementStore.MinimumHeight);
        _normal = Fit(saved);
        _maximized = saved.Maximized;
        SetMinimum();
        app.MoveAndResize(new RectInt32(_normal.X, _normal.Y, _normal.Width, _normal.Height));
        if (saved.Maximized && app.Presenter is OverlappedPresenter presenter) presenter.Maximize();
        app.Changed += Changed;
        app.Closing += Closing;
        _timer.Tick += (_, _) => { _timer.Stop(); Save(); };
        window.Closed += (_, _) =>
        {
            _timer.Stop();
            app.Changed -= Changed;
            app.Closing -= Closing;
            Controllers.Remove(window);
        };
    }

    private WindowPlacement Fit(WindowPlacement placement)
    {
        // A display left of/above the primary cannot satisfy nonnegative coordinates.
        var displays = DisplayArea.FindAll();
        var area = displays.FirstOrDefault(display =>
        {
            var work = display.WorkArea;
            return work.X >= 0 && work.Y >= 0 && placement.X >= work.X && placement.Y >= work.Y &&
                (long)placement.X < (long)work.X + work.Width && (long)placement.Y < (long)work.Y + work.Height;
        }) ?? DisplayArea.Primary;
        var bounds = area.WorkArea;
        return WindowPlacementStore.Fit(placement, bounds.X, bounds.Y, bounds.Width, bounds.Height);
    }

    private void SetMinimum()
    {
        if (_window.AppWindow.Presenter is not OverlappedPresenter presenter) return;
        var work = DisplayArea.GetFromWindowId(_window.AppWindow.Id, DisplayAreaFallback.Primary).WorkArea;
        presenter.PreferredMinimumWidth = Math.Min(WindowPlacementStore.MinimumWidth, work.Width);
        presenter.PreferredMinimumHeight = Math.Min(WindowPlacementStore.MinimumHeight, work.Height);
    }

    private void Changed(AppWindow app, AppWindowChangedEventArgs args)
    {
        if (_adjusting) return;
        if (app.Presenter is OverlappedPresenter active && active.State != OverlappedPresenterState.Minimized)
            _maximized = active.State == OverlappedPresenterState.Maximized;
        if (app.Presenter is OverlappedPresenter { State: OverlappedPresenterState.Restored })
        {
            _adjusting = true;
            try
            {
                SetMinimum();
                var current = new WindowPlacement(app.Position.X, app.Position.Y, app.Size.Width, app.Size.Height);
                // Allow dragging across displays; full work-area fitting happens on save/restore.
                _normal = current with { X = Math.Max(0, current.X), Y = Math.Max(0, current.Y) };
                if (current != _normal)
                    app.MoveAndResize(new RectInt32(_normal.X, _normal.Y, _normal.Width, _normal.Height));
            }
            finally { _adjusting = false; }
        }
        _timer.Stop();
        _timer.Start();
    }

    private void Save()
    {
        // Minimize must not replace the last normal bounds or maximized preference.
        _store.Save(Fit(_normal) with { Maximized = _maximized });
    }

    private void Closing(AppWindow app, AppWindowClosingEventArgs args)
    {
        _timer.Stop();
        Save();
    }
}
