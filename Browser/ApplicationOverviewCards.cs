// SPDX-License-Identifier: GPL-3.0-only
using Microsoft.UI.Reactor;
using Microsoft.UI.Reactor.Core;
using Microsoft.UI.Xaml;
using WinApps.Models;
using static Microsoft.UI.Reactor.Factories;
using static WinApps.Browser.BrowserUi;

namespace WinApps.Browser;

internal sealed record ApplicationOverviewCardsProps(InstalledApp App, PackageConfiguration? Package,
    bool Compact, bool Busy, Action<string> OnView, Action<string> OnOpenFolder);

internal sealed class ApplicationOverviewCards : Component<ApplicationOverviewCardsProps>
{
    private string _section = "Application";
    private readonly Dictionary<string, FrameworkElement> _panels = new();
    private readonly Dictionary<string, Microsoft.UI.Xaml.Controls.Button> _buttons = new();
    private bool _selecting;
    private sealed class InputAction { public Action? Select; }
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Microsoft.UI.Xaml.Controls.Button, InputAction> Inputs = new();
    private void AttachInput(Microsoft.UI.Xaml.Controls.Button button, string section)
    {
        if (Inputs.TryGetValue(button, out var input)) { input.Select = () => SelectSection(section); return; }
        input = new InputAction { Select = () => SelectSection(section) };
        Inputs.Add(button, input);
        button.Click += (_, _) => input.Select?.Invoke();
    }

    private void SelectSection(string section)
    {
        if (_selecting) return;
        _selecting = true;
        try
        {
            _section = section;
            foreach (var (name, panel) in _panels)
                panel.Visibility = name == section ? Visibility.Visible : Visibility.Collapsed;
            foreach (var (name, button) in _buttons)
            {
                BrowserMotion.CardSelectionFeedback(button, name == section);
                Microsoft.UI.Xaml.Automation.AutomationProperties.SetItemStatus(button, name == section ? "Selected" : "");
            }
            if (_panels.TryGetValue(section, out var selectedPanel))
                selectedPanel.DispatcherQueue.TryEnqueue(() =>
                {
                    // Reveal after visibility changes have reached the layout pass.
                    if (_section != section || selectedPanel.XamlRoot is null) return;
                    selectedPanel.StartBringIntoView(new BringIntoViewOptions
                    {
                        AnimationDesired = new Windows.UI.ViewManagement.UISettings().AnimationsEnabled,
                        VerticalAlignmentRatio = 0,
                        VerticalOffset = -12
                    });
                });
        }
        finally { _selecting = false; }
    }

    public override Element Render()
    {
        var app = Props.App;
        var package = Props.Package;
        var packaged = !string.IsNullOrWhiteSpace(app.FamilyName);
        Element Card(string title, string glyph, string summary, string subtitle) => GlassButton(Button(HStack(8,
            FluentGlyph(glyph, 16), TextBlock(title).SemiBold()), () => { }))
            .Padding(10).CornerRadius(9).Height(44).MinWidth(0).HAlign(HorizontalAlignment.Stretch)
            .Set(control =>
            {
                _buttons[title] = control;
                AttachInput(control, title);
                control.HorizontalContentAlignment = HorizontalAlignment.Center;
                Microsoft.UI.Xaml.Controls.ToolTipService.SetToolTip(control, $"{summary}\n{subtitle}");
                control.VerticalContentAlignment = VerticalAlignment.Center;
                BrowserMotion.CardSelectionFeedback(control, _section == title);
                Microsoft.UI.Xaml.Automation.AutomationProperties.SetItemStatus(control, _section == title ? "Selected" : "");
            })
            .Margin(right: title == "Files" ? 0 : 8).Flex(grow: 1, shrink: 1, basis: 0)
            .AutomationName($"Show {title} details").WithKey(title);
        var cards = new[]
        {
            Card("Application", "\uE71D", app.Name, $"Version {app.Version}"),
            Card("Package", "\uE7B8", packaged ? "Packaged application" : "Desktop application",
                packaged ? "Manifest, trust & framework" : "Registration and app identity"),
            Card("Files", "\uE8B7", "Installed files & app data", "View locations and open folders")
        };
        return VStack(12,
            FlexRow() with { Children = cards },
            VStack(0, new[] { "Application", "Package", "Files" }.Select(name =>
                VStack(0, Component<ApplicationOverviewDetails, ApplicationOverviewDetailsProps>(new(Props, name))
                    .WithKey($"details-{app.FullName}-{name}"))
                    .Set(control =>
                    {
                        _panels[name] = control;
                        control.Visibility = _section == name ? Visibility.Visible : Visibility.Collapsed;
                    }).WithKey($"panel-{name}")).ToArray()).WithKey("overview-details"));
    }
}
