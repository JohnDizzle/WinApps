// SPDX-License-Identifier: GPL-3.0-only
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Hosting;
using Windows.UI.ViewManagement;

namespace WinApps.Browser;

internal static class BrowserMotion
{
    internal static event Action? ActivityListAcknowledged;
    internal static void AcknowledgeActivityList() => ActivityListAcknowledged?.Invoke();
    private sealed class NewActivityState
    {
        public bool Active;
        public Microsoft.UI.Xaml.Media.Brush? NormalBrush;
        public Thickness NormalThickness;
        public DispatcherTimer Timer = new() { Interval = TimeSpan.FromMilliseconds(180) };
        public Microsoft.UI.Xaml.Media.LinearGradientBrush Rainbow = new();
        public int Phase;
    }
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Border, NewActivityState> NewActivityStates = new();
    public static void NewActivityBorder(Border border, bool active)
    {
        if (!NewActivityStates.TryGetValue(border, out var state))
        {
            state = new NewActivityState { NormalBrush = border.BorderBrush, NormalThickness = new Thickness(1) };
            var colors = new[] { Microsoft.UI.Colors.Magenta, Microsoft.UI.Colors.Orange, Microsoft.UI.Colors.Yellow,
                Microsoft.UI.Colors.LimeGreen, Microsoft.UI.Colors.DeepSkyBlue, Microsoft.UI.Colors.BlueViolet, Microsoft.UI.Colors.Magenta };
            state.Rainbow.StartPoint = new(0, 0);
            state.Rainbow.EndPoint = new(1, 1);
            for (var index = 0; index < colors.Length; index++)
                state.Rainbow.GradientStops.Add(new Microsoft.UI.Xaml.Media.GradientStop { Color = colors[index], Offset = index / (double)(colors.Length - 1) });
            state.Timer.Tick += (_, _) =>
            {
                state.Phase = (state.Phase + 1) % (colors.Length - 1);
                for (var index = 0; index < colors.Length; index++)
                    state.Rainbow.GradientStops[index].Color = colors[(index + state.Phase) % (colors.Length - 1)];
            };
            border.Unloaded += (_, _) => state.Timer.Stop();
            border.Loaded += (_, _) => { if (state.Active && new UISettings().AnimationsEnabled) state.Timer.Start(); };
            NewActivityStates.Add(border, state);
        }
        state.Active = active;
        state.Timer.Stop();
        border.BorderBrush = active && !new AccessibilitySettings().HighContrast ? state.Rainbow : state.NormalBrush;
        border.BorderThickness = active ? new Thickness(2) : state.NormalThickness;
        if (active && border.IsLoaded && new UISettings().AnimationsEnabled && !new AccessibilitySettings().HighContrast) state.Timer.Start();
    }
    private sealed class CardFeedback
    {
        public bool Selected;
        public Microsoft.UI.Xaml.Media.Brush? NormalStroke;
        public Thickness NormalThickness;
        public Microsoft.UI.Xaml.Media.ScaleTransform Scale = new();
        public Microsoft.UI.Xaml.Media.Brush? AccentStroke;
    }
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<FrameworkElement, CardFeedback> CardFeedbackStates = new();
    public static void CardSelectionFeedback(FrameworkElement control, bool selected)
    {
        if (!CardFeedbackStates.TryGetValue(control, out var state))
        {
            state = new CardFeedback();
            state.AccentStroke = new Microsoft.UI.Xaml.Media.SolidColorBrush(new UISettings().GetColorValue(
                new Windows.UI.ViewManagement.AccessibilitySettings().HighContrast ? UIColorType.Foreground : UIColorType.Accent));
            if (control is Control native) { state.NormalStroke = native.BorderBrush; state.NormalThickness = native.BorderThickness; }
            else if (control is Border border) { state.NormalStroke = border.BorderBrush; state.NormalThickness = border.BorderThickness; }
            CardFeedbackStates.Add(control, state);
            control.PointerPressed += (_, _) => ApplyCardFeedback(control, state, true);
            control.PointerReleased += (_, _) => ApplyCardFeedback(control, state, state.Selected);
            control.PointerCanceled += (_, _) => ApplyCardFeedback(control, state, state.Selected);
            control.PointerCaptureLost += (_, _) => ApplyCardFeedback(control, state, state.Selected);
        }
        state.Selected = selected;
        ApplyCardFeedback(control, state, selected);
    }
    private static void ApplyCardFeedback(FrameworkElement control, CardFeedback state, bool inset)
    {
        state.Scale.ScaleX = inset ? 0.98 : 1;
        state.Scale.ScaleY = inset ? 0.98 : 1;
        control.RenderTransform = state.Scale;
        control.RenderTransformOrigin = new Windows.Foundation.Point(0.5, 0.5);
        var stroke = inset ? state.AccentStroke : state.NormalStroke;
        var thickness = inset ? new Thickness(2) : state.NormalThickness;
        if (control is Control native) { native.BorderBrush = stroke; native.BorderThickness = thickness; }
        else if (control is Border border) { border.BorderBrush = stroke; border.BorderThickness = thickness; }
    }

    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<ScrollView, object> WheelAttached = new();
    public static void HorizontalMouseWheel(ScrollView control)
    {
        if (WheelAttached.TryGetValue(control, out _)) return;
        WheelAttached.Add(control, new object());
        control.AddHandler(UIElement.PointerWheelChangedEvent,
            new Microsoft.UI.Xaml.Input.PointerEventHandler((_, args) =>
            {
                var point = args.GetCurrentPoint(control);
                if (point.Properties.IsHorizontalMouseWheel || control.ScrollableWidth <= 0) return;
                var next = Math.Clamp(control.HorizontalOffset - point.Properties.MouseWheelDelta / 120d * 158,
                    0, control.ScrollableWidth);
                // At either end, let the containing page continue scrolling vertically.
                if (Math.Abs(next - control.HorizontalOffset) < 0.5) return;
                control.ScrollTo(next, 0, new ScrollingScrollOptions(
                    ScrollingAnimationMode.Disabled, ScrollingSnapPointsMode.Ignore));
                args.Handled = true;
            }), true);
    }

    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<FrameworkElement, object> SlideAttached = new();
    public static void SlideEntrance(FrameworkElement control)
    {
        if (SlideAttached.TryGetValue(control, out _)) return;
        SlideAttached.Add(control, new object());
        control.Loaded += (_, _) =>
        {
            if (!new UISettings().AnimationsEnabled) return;
            ElementCompositionPreview.SetIsTranslationEnabled(control, true);
            var visual = ElementCompositionPreview.GetElementVisual(control);
            var slide = visual.Compositor.CreateVector3KeyFrameAnimation();
            slide.Duration = TimeSpan.FromMilliseconds(220);
            slide.InsertKeyFrame(0, new System.Numerics.Vector3(24, 0, 0));
            slide.InsertKeyFrame(1, System.Numerics.Vector3.Zero);
            visual.StartAnimation("Translation", slide);
        };
    }
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<FrameworkElement, object> Attached = new();
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<FrameworkElement, object> LoadingAttached = new();
    private sealed class LayoutState { public bool Compact; }
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<FrameworkElement, LayoutState> LayoutStates = new();
    private sealed class SelectionState { public bool Selected; }
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<FrameworkElement, SelectionState> SelectionStates = new();
    private sealed class AppearanceState { public string Value = ""; }
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<FrameworkElement, AppearanceState> AppearanceStates = new();

    public static void SelectionTransition(FrameworkElement control, bool selected)
    {
        if (!SelectionStates.TryGetValue(control, out var state))
        {
            SelectionStates.Add(control, new SelectionState { Selected = selected });
            return;
        }
        if (state.Selected == selected) return;
        state.Selected = selected;
        Fade(control);
    }

    public static void AppearanceTransition(FrameworkElement control, string value)
    {
        if (!AppearanceStates.TryGetValue(control, out var state))
        {
            AppearanceStates.Add(control, new AppearanceState { Value = value });
            return;
        }
        if (state.Value == value) return;
        state.Value = value;
        Fade(control);
    }

    public static void LayoutTransition(FrameworkElement control, bool compact)
    {
        if (!LayoutStates.TryGetValue(control, out var state))
        {
            LayoutStates.Add(control, new LayoutState { Compact = compact });
            return;
        }
        if (state.Compact == compact) return;
        state.Compact = compact;
        Fade(control);
    }

    public static void LoadingBounce(FrameworkElement control)
    {
        if (LoadingAttached.TryGetValue(control, out _)) return;
        LoadingAttached.Add(control, new object());
        control.Loaded += (_, _) =>
        {
            if (!new UISettings().AnimationsEnabled) return;
            ElementCompositionPreview.SetIsTranslationEnabled(control, true);
            var visual = ElementCompositionPreview.GetElementVisual(control);
            var animation = visual.Compositor.CreateVector3KeyFrameAnimation();
            animation.Duration = TimeSpan.FromMilliseconds(420);
            animation.InsertKeyFrame(0, System.Numerics.Vector3.Zero);
            animation.InsertKeyFrame(0.45f, new System.Numerics.Vector3(0, -4, 0));
            animation.InsertKeyFrame(1, System.Numerics.Vector3.Zero);
            visual.StartAnimation("Translation", animation);
        };
    }

    public static void Enable(FrameworkElement control)
    {
        if (Attached.TryGetValue(control, out _)) return;
        Attached.Add(control, new object());
        // Theme transitions follow Windows animation preferences and use its
        // native Fluent timing for entering and repositioning content.
        if (control is Panel panel)
            panel.ChildrenTransitions = new TransitionCollection
            {
                new EntranceThemeTransition { IsStaggeringEnabled = false },
                new RepositionThemeTransition()
            };
        if (control is ToggleButton toggle)
        {
            toggle.Checked += (_, _) => Fade(control);
            toggle.Unchecked += (_, _) => Fade(control);
        }
    }

    private static void Fade(FrameworkElement control)
    {
        if (!control.IsLoaded || !new UISettings().AnimationsEnabled) return;
        var visual = ElementCompositionPreview.GetElementVisual(control);
        var animation = visual.Compositor.CreateScalarKeyFrameAnimation();
        animation.Duration = TimeSpan.FromMilliseconds(180);
        animation.InsertKeyFrame(0, 0.8f);
        animation.InsertKeyFrame(1, 1f);
        visual.StartAnimation("Opacity", animation);
    }
}
