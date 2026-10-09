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
        button.AddHandler(UIElement.PointerPressedEvent, new Microsoft.UI.Xaml.Input.PointerEventHandler((_, args) =>
        {
            if (args.Pointer.PointerDeviceType == Microsoft.UI.Input.PointerDeviceType.Mouse && args.GetCurrentPoint(button).Properties.IsLeftButtonPressed)
                input.Select?.Invoke();
        }), true);
    }

    private void SelectSection(string section)
    {
        if (_selecting || _section == section) return;
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
        }
        finally { _selecting = false; }
    }

    public override Element Render()
    {
        var app = Props.App;
        var package = Props.Package;
        var packaged = !string.IsNullOrWhiteSpace(app.FamilyName);
        Element Card(string title, string glyph, string summary, string subtitle) => GlassButton(Button(VStack(8,
            HStack(10, FluentGlyph(glyph, 24), TextBlock(title).FontSize(18).SemiBold()).HAlign(HorizontalAlignment.Center),
            TextBlock(summary).TextWrapping().SemiBold().Set(control => { control.TextAlignment = TextAlignment.Center; control.MaxLines = 2; control.TextTrimming = TextTrimming.CharacterEllipsis; }),
            TextBlock(subtitle).TextWrapping().Foreground(Theme.SecondaryText).Set(control => { control.TextAlignment = TextAlignment.Center; control.MaxLines = 1; control.TextTrimming = TextTrimming.CharacterEllipsis; })), () => { }))
            .Padding(16).CornerRadius(12).Height(136).MinWidth(0).HAlign(HorizontalAlignment.Stretch)
            .Set(control =>
            {
                _buttons[title] = control;
                AttachInput(control, title);
                control.HorizontalContentAlignment = HorizontalAlignment.Stretch;
                control.VerticalContentAlignment = VerticalAlignment.Center;
                BrowserMotion.CardSelectionFeedback(control, _section == title);
                Microsoft.UI.Xaml.Automation.AutomationProperties.SetItemStatus(control, _section == title ? "Selected" : "");
            })
            .Margin(right: Props.Compact ? 0 : 8, bottom: Props.Compact ? 8 : 0).Flex(grow: 1, shrink: 1, basis: 0)
            .AutomationName($"Show {title} details").WithKey(title);
        var cards = new[]
        {
            Card("Application", "\uE71D", app.Name, $"Version {app.Version}"),
            Card("Package", "\uE7B8", packaged ? "Packaged application" : "Desktop application",
                packaged ? "Manifest, trust & framework" : "Registration and app identity"),
            Card("Files", "\uE8B7", "Installed files & app data", "View locations and open folders")
        };
        return VStack(12,
            (Props.Compact ? FlexColumn() : FlexRow()) with { Children = cards },
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
