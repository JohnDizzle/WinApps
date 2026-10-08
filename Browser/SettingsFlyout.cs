// SPDX-License-Identifier: GPL-3.0-only
using System.Diagnostics;
using System.Reflection;
using Microsoft.UI.Reactor;
using Microsoft.UI.Reactor.Core;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace MyApps.Browser;

internal static class SettingsFlyout
{
    internal const string PagesUrl = "https://webdivelinkscapebrowsers.blob.core.windows.net/linkwebdivebrowsers/winapps-cache/";

    internal static Flyout Create(ElementTheme theme, bool darkTheme, BackdropKind backdrop,
        Action toggleTheme, Action<BackdropKind> setBackdrop)
    {
        var content = new StackPanel { Spacing = 12, Width = 288, RequestedTheme = theme };
        content.Children.Add(new TextBlock { Text = "Settings", FontSize = 20, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
        var themeButton = new Button
        {
            Content = darkTheme ? "Switch to light theme" : "Switch to dark theme",
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        content.Children.Add(themeButton);
        content.Children.Add(new TextBlock { Text = "Backdrop", FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
        var choices = new ComboBox { HorizontalAlignment = HorizontalAlignment.Stretch };
        var kinds = new[] { BackdropKind.Mica, BackdropKind.DesktopAcrylic, BackdropKind.MicaAlt };
        foreach (var label in new[] { "Mica", "Acrylic", "Mica Alt" }) choices.Items.Add(label);
        choices.SelectedIndex = Array.IndexOf(kinds, backdrop);
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(choices, "Backdrop");
        content.Children.Add(choices);
        var resources = Microsoft.UI.Xaml.Application.Current.Resources;
        Brush Brush(string key) => (Brush)resources[key];
        content.Children.Add(new Border
        {
            Height = 1, Background = Brush("DividerStrokeColorDefaultBrush"), Margin = new Thickness(0, 4, 0, 4)
        });
        var details = new StackPanel { Spacing = 6 };
        details.Children.Add(new TextBlock { Text = "WinApps", FontSize = 18, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
        details.Children.Add(new TextBlock { Text = $"Version {Version()}", FontSize = 12, Foreground = Brush("TextFillColorSecondaryBrush") });
        details.Children.Add(new TextBlock { Text = "John M. Doyle", FontSize = 14 });
        details.Children.Add(new TextBlock { Text = "Free & open source · GPL-3.0-only", FontSize = 12, Foreground = Brush("TextFillColorSecondaryBrush") });
        var links = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Margin = new Thickness(0, 4, 0, 0) };
        var notice = new TextBlock { FontSize = 12, TextWrapping = TextWrapping.Wrap, Visibility = Visibility.Collapsed };
        void Open(string target)
        {
            try
            {
                Process.Start(new ProcessStartInfo(target) { UseShellExecute = true });
                notice.Visibility = Visibility.Collapsed;
            }
            catch (Exception)
            {
                notice.Text = "Couldn't open this page. Check your default browser and try again.";
                notice.Visibility = Visibility.Visible;
            }
        }
        var privacy = new HyperlinkButton { Content = "Privacy", Padding = new Thickness(0, 4, 8, 4) };
        privacy.Click += (_, _) => Open(PagesUrl + "privacy.html");
        var features = new HyperlinkButton { Content = "Features", Padding = new Thickness(0, 4, 8, 4) };
        features.Click += (_, _) => Open(Path.Combine(AppContext.BaseDirectory, "features.html"));
        var license = new HyperlinkButton { Content = "License", Padding = new Thickness(8, 4, 0, 4) };
        license.Click += (_, _) => Open(Path.Combine(AppContext.BaseDirectory, "license.html"));
        links.Children.Add(features);
        links.Children.Add(privacy);
        links.Children.Add(license);
        details.Children.Add(links);
        details.Children.Add(notice);
        content.Children.Add(new Border
        {
            Child = details, Padding = new Thickness(16), CornerRadius = new CornerRadius(12),
            Background = Brush("CardBackgroundFillColorDefaultBrush"),
            BorderBrush = Brush("CardStrokeColorDefaultBrush"), BorderThickness = new Thickness(1)
        });
        var flyout = new Flyout
        {
            Placement = Microsoft.UI.Xaml.Controls.Primitives.FlyoutPlacementMode.BottomEdgeAlignedRight,
            Content = new ScrollViewer { Content = content, MaxHeight = 560, VerticalScrollBarVisibility = ScrollBarVisibility.Auto }
        };
        themeButton.Click += (_, _) => { flyout.Hide(); toggleTheme(); };
        choices.SelectionChanged += (_, _) =>
        {
            if (choices.SelectedIndex >= 0) { flyout.Hide(); setBackdrop(kinds[choices.SelectedIndex]); }
        };
        return flyout;
    }

    private static string Version()
    {
        try
        {
            var version = Windows.ApplicationModel.Package.Current.Id.Version;
            return $"{version.Major}.{version.Minor}.{version.Build}.{version.Revision}";
        }
        catch (InvalidOperationException)
        {
            return Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "Unknown";
        }
    }
}
