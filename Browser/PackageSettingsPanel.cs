using Microsoft.UI.Reactor;
using Microsoft.UI.Reactor.Core;
using MyApps.Models;
using MyApps.Services.Settings;
using static Microsoft.UI.Reactor.Factories;
using static MyApps.Browser.BrowserUi;

namespace MyApps.Browser;

internal sealed record PackageSettingsPanelProps(string FamilyName, AppSettingsSnapshot? Snapshot, bool Disabled);

internal sealed class PackageSettingsPanel : Component<PackageSettingsPanelProps>
{
    public override Element Render()
    {
        var snapshot = UseState(Props.Snapshot);
        var loading = UseState(false);
        var message = UseState("");
        UseEffect(() => snapshot.Set(Props.Snapshot), Props.Snapshot);
        async void Reload()
        {
            loading.Set(true);
            try { snapshot.Set(await Task.Run(() => new PackageSettingsReader().Read(Props.FamilyName))); message.Set("Settings reloaded."); }
            catch (Exception ex) { message.Set(ex.Message); }
            finally { loading.Set(false); }
        }
        var current = snapshot.Value;
        return Section("ApplicationData values",
            Text($"{current?.Values.Count ?? 0} values / composite members"),
            Text("Edit a value, then Save. Stored types are preserved. Close the target app first; it may overwrite settings when it exits."),
            ActionButton("Reload settings · discard unsaved edits", Reload).IsEnabled(!Props.Disabled && !loading.Value),
            Text(message.Value),
            VStack(8, (current?.Values ?? []).GroupBy(item => item.Container).Select(group =>
                Disclosure(group.Key, VStack(12, group.Select(item =>
                    Component<SettingEditor, SettingEditorProps>(new(Props.FamilyName, item, current!, Props.Disabled || loading.Value))
                        .WithKey(System.Text.Json.JsonSerializer.Serialize(item.Location) + item.Name)).ToArray()), true)
                    .WithKey(group.Key)).ToArray()),
            VStack(4, (current?.Notices ?? []).Select(Text).ToArray()));
    }
}

internal sealed record SettingEditorProps(string FamilyName, AppSetting Setting, object ResetToken, bool Disabled);

internal sealed class SettingEditor : Component<SettingEditorProps>
{
    private Microsoft.UI.Xaml.Controls.NumberBox? _numberBox;
    private long _textToken;
    private Action<string>? _onText;
    private string _draft = "";

    public override Element Render()
    {
        var setting = UseState(Props.Setting);
        var value = UseState(Props.Setting.Value);
        var saving = UseState(false);
        var message = UseState("");
        _onText = value.Set;
        _draft = value.Value;
        UseEffect(() => () =>
        {
            _numberBox?.UnregisterPropertyChangedCallback(Microsoft.UI.Xaml.Controls.NumberBox.TextProperty, _textToken);
            _numberBox = null;
            _onText = null;
        }, Array.Empty<object>());
        UseEffect(() => { setting.Set(Props.Setting); value.Set(Props.Setting.Value); message.Set(""); }, Props.ResetToken);
        var editable = setting.Value.Location is not null && SettingValueCodec.CanEdit(setting.Value.Type);
        var valid = SettingValueCodec.TryParse(setting.Value.Type, value.Value, out _, out var error);
        async void Save()
        {
            if (saving.Value || Props.Disabled || !editable || !valid || value.Value == setting.Value.Value) return;
            var expected = setting.Value;
            var draft = value.Value;
            saving.Set(true);
            try
            {
                var result = await Task.Run(() => new PackageSettingsWriter().Write(Props.FamilyName, expected, draft));
                message.Set(result.Message);
                if (result.Saved)
                {
                    var updated = await Task.Run(() => new PackageSettingsReader().Read(Props.FamilyName));
                    var location = expected.Location!;
                    var saved = updated.Values.FirstOrDefault(item => item.Location is { } other &&
                        other.Store == location.Store && other.Key == location.Key && other.Containers.SequenceEqual(location.Containers));
                    if (saved is not null) { setting.Set(saved); value.Set(saved.Value); }
                }
            }
            catch (Exception ex) { message.Set($"Save failed: {ex.Message}"); }
            finally { saving.Set(false); }
        }
        Element editor = ReadOnlyText(value.Value, setting.Value.Name);
        if (editable)
        {
            if (setting.Value.Type == "Boolean")
                editor = ToggleSwitch(bool.TryParse(value.Value, out var flag) && flag, next => value.Set(next.ToString())).AutomationName(setting.Value.Name)
                    .Set(control => { control.OnContent = "True"; control.OffContent = "False"; });
            else if (setting.Value.Type is "Byte" or "Int16" or "UInt16" or "Int32" or "UInt32" or "Single" or "Double")
                editor = NumberBox().AutomationName(setting.Value.Name).Set(control =>
                {
                    control.ValidationMode = Microsoft.UI.Xaml.Controls.NumberBoxValidationMode.Disabled;
                    control.SpinButtonPlacementMode = Microsoft.UI.Xaml.Controls.NumberBoxSpinButtonPlacementMode.Compact;
                    if (!ReferenceEquals(_numberBox, control))
                    {
                        // Use invariant formatting to match the persisted type parser.
                        control.NumberFormatter = new Windows.Globalization.NumberFormatting.DecimalFormatter(["en-US"], "US")
                        { IsGrouped = false, FractionDigits = 0 };
                        _numberBox?.UnregisterPropertyChangedCallback(Microsoft.UI.Xaml.Controls.NumberBox.TextProperty, _textToken);
                        _numberBox = control;
                        _textToken = control.RegisterPropertyChangedCallback(Microsoft.UI.Xaml.Controls.NumberBox.TextProperty, (sender, _) =>
                        {
                            var text = ((Microsoft.UI.Xaml.Controls.NumberBox)sender).Text;
                            if (text != _draft) _onText?.Invoke(text);
                        });
                    }
                    if (control.Text != value.Value) control.Text = value.Value;
                });
            else if (SettingNumericValue.NeedsExactIntegerEditor(setting.Value.Type))
                editor = FlexRow(
                    TextBox(value.Value, value.Set).AutomationName(setting.Value.Name).Set(control =>
                        control.InputScope = new Microsoft.UI.Xaml.Input.InputScope
                        { Names = { new Microsoft.UI.Xaml.Input.InputScopeName { NameValue = Microsoft.UI.Xaml.Input.InputScopeNameValue.Number } } })
                        .Flex(grow: 1, basis: 0),
                    Button("−", () => { if (SettingNumericValue.TryStep(setting.Value.Type, value.Value, -1, out var next)) value.Set(next); })
                        .AutomationName($"Decrease {setting.Value.Name}").Margin(left: 6)
                        .IsEnabled(SettingNumericValue.TryStep(setting.Value.Type, value.Value, -1, out _)),
                    Button("+", () => { if (SettingNumericValue.TryStep(setting.Value.Type, value.Value, 1, out var next)) value.Set(next); })
                        .AutomationName($"Increase {setting.Value.Name}").Margin(left: 4)
                        .IsEnabled(SettingNumericValue.TryStep(setting.Value.Type, value.Value, 1, out _)));
            else
                editor = TextBox(value.Value, value.Set).AutomationName(setting.Value.Name);
        }
        return VStack(6,
            FlexRow(TextBlock(setting.Value.Name).SemiBold().TextWrapping().Flex(grow: 1, basis: 0),
                TextBlock($"Stored type: {setting.Value.Type}").FontSize(12).Foreground(Theme.SecondaryText).Margin(left: 8).Flex(shrink: 0)),
            editor.IsEnabled(!Props.Disabled && !saving.Value).AutomationName(setting.Value.Name),
            editable ? ActionButton("Save", Save).IsEnabled(!Props.Disabled && !saving.Value && valid && value.Value != setting.Value.Value) : Text("Read only"),
            Text(editable && !valid ? error : message.Value)).HAlign(Microsoft.UI.Xaml.HorizontalAlignment.Stretch);
    }
}
