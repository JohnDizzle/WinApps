// SPDX-License-Identifier: GPL-3.0-only
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
