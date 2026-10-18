// SPDX-License-Identifier: GPL-3.0-only
using System.Diagnostics;
using System.Reflection;
using Microsoft.UI.Reactor;
using Microsoft.UI.Reactor.Core;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using WinApps.Services.Storage;

namespace WinApps.Browser;

internal static class SettingsFlyout
{
    internal const string PagesUrl = "https://webdivelinkscapebrowsers.blob.core.windows.net/linkwebdivebrowsers/winapps-cache/";

    internal static Flyout Create(ElementTheme theme, bool darkTheme, BackdropKind backdrop,
        Action toggleTheme, Action<BackdropKind> setBackdrop)
    {
        var content = new StackPanel { Spacing = 16, Width = 400, RequestedTheme = theme };
        content.Children.Add(new TextBlock { Text = "Settings", FontSize = 20, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
        var themeButton = new Button
        {
            Content = darkTheme ? "Switch to light theme" : "Switch to dark theme",
            HorizontalAlignment = HorizontalAlignment.Stretch
        };

        var choices = new ComboBox { Header = "Window material", HorizontalAlignment = HorizontalAlignment.Stretch };
        var kinds = new[] { BackdropKind.Mica, BackdropKind.DesktopAcrylic, BackdropKind.MicaAlt };
        foreach (var label in new[] { "Mica", "Acrylic", "Mica Alt" }) choices.Items.Add(label);
        choices.SelectedIndex = Array.IndexOf(kinds, backdrop);
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(choices, "Backdrop");
        content.Children.Add(SettingsCard("Appearance", "\uE790", themeButton, choices));
        var startCompact = new ToggleSwitch
        {
            Header = "Start in Mini WinApps",
            IsOn = CompactWindowStore.Shared.Load().StartInCompactMode
        };
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(startCompact, "Start in Mini WinApps");
        startCompact.Toggled += (_, _) => CompactWindowStore.Shared.SetStartInCompactMode(startCompact.IsOn);
        var rememberCompact = new ToggleSwitch
        {
            Header = "Remember position and size",
            IsOn = CompactWindowStore.Shared.Load().RememberPrevious
        };
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(rememberCompact, "Remember compact view position and size");
        rememberCompact.Toggled += (_, _) => CompactWindowStore.Shared.SetRememberPrevious(rememberCompact.IsOn);
        content.Children.Add(SettingsCard("Compact view", "\uE8A7",
            ToggleRow("Start in Mini WinApps", "Use compact view when the app starts.", startCompact),
            ToggleRow("Remember position and size", "Reopen where you last left it.", rememberCompact)));
        var history = ApplicationViewHistory.Shared;
        var learning = new ToggleSwitch { Header = "Log app interactions", IsOn = history.Policy.Enabled };
        var retention = new ComboBox { Header = "Keep daily history", HorizontalAlignment = HorizontalAlignment.Stretch };
        var days = new[] { 7, 30, 90, history.Policy.RetentionDays }.Distinct().Order().ToArray();
        foreach (var day in days) retention.Items.Add($"{day} days");
        retention.SelectedIndex = Array.IndexOf(days, history.Policy.RetentionDays);
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(retention, "Suggestion history retention");

        var policyLink = new HyperlinkButton { Content = "Read suggestion logging policy", Padding = new Thickness(0, 4, 0, 4) };
        policyLink.Click += (_, _) => SuggestionPolicyNotice.OpenPolicy();
        var suggestionMetrics = new TextBlock { TextWrapping = TextWrapping.Wrap, FontSize = 12 };
        BindTheme(suggestionMetrics, ("Foreground", "TextFillColorSecondaryBrush"));
        void RefreshSuggestionMetrics()
        {
            var metrics = history.Metrics();
            suggestionMetrics.Text = metrics.HitRate is { } rate
                ? $"{metrics.Hits} of {metrics.Shown} suggestion sets selected · {rate:P0} hit rate"
                : "No suggestion results recorded yet.";
        }
        RefreshSuggestionMetrics();
        var clearHistory = new Button { Content = "Clear suggestion history", HorizontalAlignment = HorizontalAlignment.Stretch };
        content.Children.Add(SettingsCard("App suggestions", "\uE8A5",
            ToggleRow("Log app interactions", "Takes effect immediately. History stays on this PC.", learning),
            retention, suggestionMetrics, policyLink, clearHistory));
        learning.Toggled += (_, _) =>
        {
            history.SetPolicy(history.Policy with { Enabled = learning.IsOn });
            RefreshSuggestionMetrics();
        };
        retention.SelectionChanged += (_, _) =>
        {
            if (retention.SelectedIndex < 0) return;
            history.SetPolicy(history.Policy with { RetentionDays = days[retention.SelectedIndex] });
            RefreshSuggestionMetrics();
        };
        clearHistory.Click += (_, _) => { history.Clear(); RefreshSuggestionMetrics(); };
        var details = new StackPanel { Spacing = 6 };
        details.Children.Add(new TextBlock { Text = "WinApps", FontSize = 18, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
        var versionLabel = new TextBlock { Text = $"Version {Version()}", FontSize = 12 };
        BindTheme(versionLabel, ("Foreground", "TextFillColorSecondaryBrush"));
        details.Children.Add(versionLabel);
        details.Children.Add(new TextBlock { Text = "John M. Doyle", FontSize = 14 });
        var licenseLabel = new TextBlock { Text = "Free & open source · GPL-3.0-only", FontSize = 12 };
        BindTheme(licenseLabel, ("Foreground", "TextFillColorSecondaryBrush"));
        details.Children.Add(licenseLabel);
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
        content.Children.Add(SettingsCard("About", "\uE946", details));
        var flyout = new Flyout
        {
            Placement = Microsoft.UI.Xaml.Controls.Primitives.FlyoutPlacementMode.BottomEdgeAlignedRight,
            Content = new ScrollViewer
            {
                Content = content, MaxHeight = 640, Padding = new Thickness(4, 4, 20, 12),
                HorizontalScrollMode = ScrollMode.Disabled, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto
            }
        };
        themeButton.Click += (_, _) => { flyout.Hide(); toggleTheme(); };
        choices.SelectionChanged += (_, _) =>
        {
            if (choices.SelectedIndex >= 0) { flyout.Hide(); setBackdrop(kinds[choices.SelectedIndex]); }
        };
        return flyout;
    }

    private static Border SettingsCard(string title, string glyph, params UIElement[] controls)
    {
        var body = new StackPanel { Spacing = 14 };
        var heading = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
        heading.Children.Add(new FontIcon { Glyph = glyph, FontSize = 18 });
        heading.Children.Add(new TextBlock { Text = title, FontSize = 16, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
        body.Children.Add(heading);
        foreach (var control in controls) body.Children.Add(control);
        var card = new Border
        {
            Child = body, Padding = new Thickness(16), CornerRadius = new CornerRadius(12), BorderThickness = new Thickness(1)
        };
        BindTheme(card, ("Background", "CardBackgroundFillColorDefaultBrush"), ("BorderBrush", "CardStrokeColorDefaultBrush"));
        return card;
    }

    private static Grid ToggleRow(string title, string description, ToggleSwitch toggle)
    {
        toggle.Header = null;
        toggle.OnContent = "";
        toggle.OffContent = "";
        toggle.MinWidth = 0;
        toggle.VerticalAlignment = VerticalAlignment.Center;
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(toggle, title);
        var row = new Grid { ColumnSpacing = 16 };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var label = new StackPanel { Spacing = 4, VerticalAlignment = VerticalAlignment.Center };
        label.Children.Add(new TextBlock { Text = title, TextWrapping = TextWrapping.Wrap });
        var hint = new TextBlock { Text = description, FontSize = 12, TextWrapping = TextWrapping.Wrap };
        BindTheme(hint, ("Foreground", "TextFillColorSecondaryBrush"));
        label.Children.Add(hint);
        row.Children.Add(label);
        Grid.SetColumn(toggle, 1);
        row.Children.Add(toggle);
        return row;
    }

    // Use native ThemeResource setters so popup colors follow light/dark and contrast changes.
    private static void BindTheme(FrameworkElement control, params (string Property, string Resource)[] bindings)
    {
        var setters = string.Concat(bindings.Select(binding =>
            $"<Setter Property='{binding.Property}' Value='{{ThemeResource {binding.Resource}}}' />"));
        control.Style = (Style)Microsoft.UI.Xaml.Markup.XamlReader.Load(
            $"<Style xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' TargetType='{control.GetType().Name}'>{setters}</Style>");
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
