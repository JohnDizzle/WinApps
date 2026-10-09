// SPDX-License-Identifier: GPL-3.0-only
using Microsoft.UI.Reactor;
using Microsoft.UI.Reactor.Core;
using static Microsoft.UI.Reactor.Factories;
using static WinApps.Browser.BrowserUi;

namespace WinApps.Browser;

internal sealed record ErrorRecoveryPanelProps(string Title, Exception Error, Action Retry, Action? Back);

internal sealed class ErrorRecoveryPanel : Component<ErrorRecoveryPanelProps>
{
    public override Element Render()
    {
        var message = UseState("");
        void Restart()
        {
            try
            {
                var reason = Microsoft.Windows.AppLifecycle.AppInstance.Restart("");
                message.Set($"Windows couldn't restart WinApps ({reason}). Close and reopen the app, or try loading this view again.");
            }
            catch (Exception ex) { message.Set($"Couldn't restart WinApps: {ex.Message}"); }
        }
        void Copy()
        {
            try
            {
                var data = new Windows.ApplicationModel.DataTransfer.DataPackage();
                data.SetText($"WinApps\n{Props.Error}");
                Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(data);
                message.Set("Technical details copied.");
            }
            catch { message.Set("Couldn't copy the details. You can select the text below."); }
        }
        return ScrollView(VStack(16,
            Card(VStack(14,
                FluentGlyph("\uE7BA", 32).HAlign(Microsoft.UI.Xaml.HorizontalAlignment.Left),
                TextBlock(Props.Title).FontSize(24).SemiBold().TextWrapping(),
                Text("A display problem interrupted this area. Try loading it again, or return to your application list."),
                VStack(8,
                    ActionButton("Try again", Props.Retry),
                    ActionButton("Restart WinApps", Restart).Set(control =>
                        Microsoft.UI.Xaml.Controls.ToolTipService.SetToolTip(control, "Restart WinApps. Unsaved edits will be cleared.")),
                    Slot("back-action", Props.Back is { } back ? ActionButton("Open Apps List", back) : Text(""))),
                TextBlock(message.Value).TextWrapping().Foreground(Theme.SecondaryText),
                Disclosure("Technical details", VStack(10,
                    Text("These details may include local paths. Review them before sharing."),
                    ActionButton("Copy details", Copy),
                    ReadOnlyText(Props.Error.ToString(), "Error technical details")), false)
            ).Padding(24)).Background(Theme.CardBackground).WithBorder(Theme.CardStroke).CornerRadius(14)
        ).Padding(24));
    }
}
