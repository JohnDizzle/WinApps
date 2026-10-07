// SPDX-License-Identifier: GPL-3.0-only
using System.Diagnostics;
using Microsoft.UI.Xaml;
using Microsoft.UI.Reactor;
using Microsoft.UI.Reactor.Core;
using MyApps.Models;
using MyApps.Services.Inventory;
using MyApps.Services.Storage;
using MyApps.Services.Settings;
using MyApps.Services.Packages;
using static Microsoft.UI.Reactor.Factories;

using static MyApps.Browser.BrowserUi;

namespace MyApps.Browser;

public sealed class PackageExplorer : Component
{
    private int _selectionVersion;
    public override Element Render()
    {
        var apps = UseState(Array.Empty<InstalledApp>());
        var selected = UseState<InstalledApp?>(null);
        var footprint = UseState(Array.Empty<FolderFootprint>());
        var inventory = UseState<AppDataInventory?>(null);
        var settings = UseState<AppSettingsSnapshot?>(null);
        var packageConfiguration = UseState<PackageConfiguration?>(null);
        var file = UseState<AppDataFile?>(null);
        var preview = UseState("");
        var view = UseState("Overview");
        var busy = UseState(false);
        var inspecting = UseState(false);
        var status = UseState("Loading your application library…");
        var railAttempt = UseState(0);
        var detailAttempt = UseState(0);
        void ClearSelection()
        {
            _selectionVersion++;
            inspecting.Set(false);
            selected.Set(null); inventory.Set(null); settings.Set(null); file.Set(null);
            packageConfiguration.Set(null);
            footprint.Set(Array.Empty<FolderFootprint>());
            preview.Set(""); view.Set("Overview");
        }

        void Unselect()
        {
            if (busy.Value) return;
            ClearSelection();
            status.Set("Application unselected. Choose an application from the list.");
        }

        async void Scan()
        {
            if (busy.Value) return;
            ClearSelection();
            busy.Set(true);
            status.Set("Discovering applications…");
            try
            {
                var result = await Task.Run(() => new AppInventory().Scan());
                apps.Set(result.Apps.ToArray());
                var cacheNotice = await Task.Run(() => new ApplicationLibraryCache().Save(result.Apps));
                status.Set($"{result.Apps.Count} applications · {result.Skipped} unavailable entries or sources skipped." +
                    (cacheNotice is null ? "" : $" {cacheNotice}"));
            }
            catch (Exception ex) { status.Set($"Scan failed: {ex.Message}"); }
            finally { busy.Set(false); }
        }

        async void LoadLibrary()
        {
            var needsScan = false;
            busy.Set(true);
            try
            {
                var cached = await Task.Run(() => new ApplicationLibraryCache().Load());
                if (cached is null) needsScan = true;
                else
                {
                    apps.Set(cached.Apps);
                    status.Set($"{cached.Apps.Length} cached applications · Updated {cached.SavedAt.ToLocalTime():g} · Refresh when needed.");
                }
            }
            catch (Exception ex) { status.Set($"Library could not be loaded: {ex.Message}"); }
            finally { busy.Set(false); }
            if (needsScan) Scan();
        }

        UseEffect(LoadLibrary, Array.Empty<object>());

        async void Select(InstalledApp app)
        {
            if (busy.Value) return;
            var request = ++_selectionVersion;
            selected.Set(app); inventory.Set(null); settings.Set(null); file.Set(null); preview.Set("");
            packageConfiguration.Set(null);
            footprint.Set(Array.Empty<FolderFootprint>()); view.Set("Overview");
            inspecting.Set(true); status.Set($"Loading {app.Name}…");
            try
            {
                var configurationTask = Task.Run(() => new PackageConfigurationReader().Read(app));
                var filesTask = Task.Run(() => new AppDataInspector().Inspect(app));
                var settingsTask = Task.Run(() => new PackageSettingsReader().Read(app.FamilyName));
                await Task.WhenAll(configurationTask, filesTask, settingsTask);
                if (request != _selectionVersion) return;
                packageConfiguration.Set(configurationTask.Result);
                inventory.Set(filesTask.Result);
                settings.Set(settingsTask.Result);
                status.Set("Disk inventory loaded. Select a file to preview supported text formats.");
            }
            catch (Exception ex) { if (request == _selectionVersion) status.Set($"Inspection failed: {ex.Message}"); }
            finally { if (request == _selectionVersion) inspecting.Set(false); }
        }

        async void Measure()
        {
            if (busy.Value || inspecting.Value || selected.Value is null) return;
            var app = selected.Value;
            busy.Set(true);
            try
            {
                footprint.Set((await Task.Run(() => new FootprintScanner().Scan(app))).ToArray());
                status.Set("Logical file bytes measured; shared dependencies and unknown data locations are excluded.");
            }
            catch (Exception ex) { status.Set($"Measurement failed: {ex.Message}"); }
            finally { busy.Set(false); }
        }

        async void PreviewFile(AppDataFile item)
        {
            if (busy.Value || inspecting.Value) return;
            file.Set(item); view.Set(item.Area); preview.Set("Loading preview…"); busy.Set(true);
            try
            {
                if (item.Area == "Settings" && Path.GetFileName(item.FullPath).Equals("settings.dat", StringComparison.OrdinalIgnoreCase)
                    && selected.Value is not null)
                {
                    var family = selected.Value.FamilyName;
                    settings.Set(await Task.Run(() => new PackageSettingsReader().Read(family)));
                    preview.Set("Decoded package values are displayed above, grouped by settings container. Windows reads the live ApplicationData store for this package; this is not an offline file parser.");
                }
                else preview.Set(await Task.Run(() => new AppDataInspector().Preview(item)));
            }
            catch (Exception ex) { preview.Set($"Preview unavailable: {ex.Message}"); }
            finally { busy.Set(false); }
        }

        void OpenFolder(string path)
        {
            try
            {
                if (!Directory.Exists(path)) { status.Set("Folder is unavailable or does not exist."); return; }
                Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
            }
            catch (Exception ex) { status.Set($"Cannot open folder: {ex.Message}"); }
        }

        void OpenFile(AppDataFile item)
        {
            try
            {
                if (!File.Exists(item.FullPath)) { status.Set("File is unavailable or no longer exists."); return; }
                Process.Start(new ProcessStartInfo(item.FullPath) { UseShellExecute = true, Verb = "open" });
                status.Set($"Opened {item.RelativePath} with its default application.");
            }
            catch (Exception ex) { status.Set($"Cannot open file with its default application: {ex.Message}"); }
        }

        void OpenView(string next) { view.Set(next); file.Set(null); }
        void RecoverList()
        {
            busy.Set(false);
            ClearSelection();
            railAttempt.Set(railAttempt.Value + 1);
            detailAttempt.Set(detailAttempt.Value + 1);
            status.Set("Choose an application from the list.");
        }
        return FlexColumn(
            FlexRow(
                ErrorBoundary(Component<ApplicationRail, ApplicationRailProps>(new(apps.Value, selected.Value, inventory.Value,
                    view.Value, busy.Value, Select, Unselect, Scan, PreviewFile, OpenFile, OpenView, inspecting.Value)).WithKey($"application-rail-{railAttempt.Value}"),
                    error => Component<ErrorRecoveryPanel, ErrorRecoveryPanelProps>(new("Application list unavailable", error,
                        () => railAttempt.Set(railAttempt.Value + 1), RecoverList)).Width(360))
                    .WithKey("rail-boundary").Flex(shrink: 0),
                ErrorBoundary(Component<ApplicationDetailPane, ApplicationDetailPaneProps>(new(selected.Value, footprint.Value,
                    inventory.Value, settings.Value, packageConfiguration.Value, file.Value, preview.Value,
                    view.Value, busy.Value || inspecting.Value, status.Value, OpenView, Measure, OpenFolder, OpenFile, PreviewFile, inspecting.Value))
                    .WithKey($"application-detail-{detailAttempt.Value}"),
                    error => Component<ErrorRecoveryPanel, ErrorRecoveryPanelProps>(new("Application details unavailable", error,
                        () => detailAttempt.Set(detailAttempt.Value + 1), RecoverList)))
                    .WithKey("detail-boundary").Flex(grow: 1, basis: 0)
            ).Flex(grow: 1, basis: 0)
        );
    }
}
