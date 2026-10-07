// SPDX-License-Identifier: GPL-3.0-only
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Hosting;
using Windows.UI.ViewManagement;

namespace MyApps.Browser;

internal static class BrowserMotion
{
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<FrameworkElement, object> Attached = new();
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<FrameworkElement, object> LoadingAttached = new();
    private sealed class LayoutState { public bool Compact; }
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<FrameworkElement, LayoutState> LayoutStates = new();

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
