// SPDX-License-Identifier: GPL-3.0-only
using Microsoft.UI.Reactor;
using Microsoft.UI.Reactor.Core;
using WinApps.Models;
using WinApps.Services.Settings;
using static Microsoft.UI.Reactor.Factories;
using static WinApps.Browser.BrowserUi;

namespace WinApps.Browser;

internal sealed record PackageSettingsPanelProps(string FamilyName, AppSettingsSnapshot? Snapshot, bool Disabled);

internal sealed class PackageSettingsPanel : Component<PackageSettingsPanelProps>
{
    public override Element Render()
    {
        var snapshot = UseState(Props.Snapshot);
        var loading = UseState(false);
        var message = UseState("");
        var noticeVersion = UseState(0);
        void ShowMessage(string text) { noticeVersion.Set(noticeVersion.Value + 1); message.Set(text); }
        UseEffect(() => snapshot.Set(Props.Snapshot), Props.Snapshot);
        async void Reload()
        {
            loading.Set(true);
            ShowMessage("");
            try { snapshot.Set(await Task.Run(() => new PackageSettingsReader().Read(Props.FamilyName))); ShowMessage("Settings reloaded."); }
            catch (Exception ex) { ShowMessage(ex.Message); }
            finally { loading.Set(false); }
        }
        var current = snapshot.Value;
        return Section("ApplicationData values",
            Text($"{current?.Values.Count ?? 0} values / composite members"),
            Text("Edit a value, then Save. Stored types are preserved. Close the target app first; it may overwrite settings when it exits."),
            ActionButton("Reload settings · discard unsaved edits", Reload).IsEnabled(!Props.Disabled && !loading.Value),
            Component<TransientActionNotice, TransientActionNoticeProps>(new(message.Value, noticeVersion.Value)).WithKey("settings-notice"),
            VStack(8, (current?.Values ?? []).GroupBy(item => item.Container).Select(group =>
                Disclosure(group.Key, VStack(4, new Element[] {
                    FlexRow(TextBlock("Key").SemiBold().Width(220).Flex(shrink: 0),
                        TextBlock("Value").SemiBold().Margin(left: 16).Flex(grow: 1, basis: 0),
                        TextBlock("Action").Width(90).Margin(left: 12).Flex(shrink: 0)).Padding(10)
                    }.Concat(group.Select(item =>
                    Component<SettingEditor, SettingEditorProps>(new(Props.FamilyName, item, current!, Props.Disabled || loading.Value))
                        .WithKey(System.Text.Json.JsonSerializer.Serialize(item.Location) + item.Name))).ToArray()), true)
                    .WithKey(group.Key)).ToArray()),
            VStack(4, (current?.Notices ?? []).Select(Text).ToArray()));
    }
}
