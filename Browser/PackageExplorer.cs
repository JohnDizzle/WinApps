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

public sealed record PackageExplorerProps(bool NavigationVisible = true);

public sealed class PackageExplorer : Component<PackageExplorerProps>
{
    private int _selectionVersion;
    private bool _compactLayout;
    private CancellationTokenSource? _inspectionCancellation;
    private CancellationTokenSource? _actionCancellation;
    private sealed record CachedApplication(PackageConfiguration? Configuration, AppDataInventory Inventory,
        AppSettingsSnapshot Settings, FolderFootprint[] Footprint);
    private readonly Dictionary<string, CachedApplication> _applicationCache = new(StringComparer.Ordinal);
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
        var actionBusy = UseState(false);
        var status = UseState("Loading your application library…");
        var railAttempt = UseState(0);
        var detailAttempt = UseState(0);
        var compact = UseState(false);
        var actionNotice = UseState("");
        var actionNoticeVersion = UseState(0);
        void NotifyAction(string text) { actionNoticeVersion.Set(actionNoticeVersion.Value + 1); actionNotice.Set(text); }
        void ClearSelection()
        {
            _selectionVersion++;
            _inspectionCancellation?.Cancel();
            _actionCancellation?.Cancel();
            actionBusy.Set(false);
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

        async void Uninstalled(InstalledApp app)
        {
            var remaining = apps.Value.Where(item => item.FamilyName != app.FamilyName).ToArray();
            foreach (var item in apps.Value.Where(item => item.FamilyName == app.FamilyName))
                _applicationCache.Remove(item.FullName);
            apps.Set(remaining);
            if (selected.Value?.FamilyName == app.FamilyName) ClearSelection();
            NotifyAction($"{app.Name} uninstalled.");
            status.Set($"{remaining.Length} applications in your library.");
            var cacheNotice = await Task.Run(() => new ApplicationLibraryCache().Save(remaining));
            if (cacheNotice is not null) NotifyAction(cacheNotice);
        }

        async void Scan()
        {
            if (busy.Value) return;
            _applicationCache.Clear();
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
            _inspectionCancellation?.Cancel();
            _actionCancellation?.Cancel();
            actionBusy.Set(false);
            using var cancellation = new CancellationTokenSource();
            _inspectionCancellation = cancellation;
            var token = cancellation.Token;
            var request = ++_selectionVersion;
            selected.Set(app); inventory.Set(null); settings.Set(null); file.Set(null); preview.Set("");
            packageConfiguration.Set(null);
            footprint.Set(Array.Empty<FolderFootprint>()); view.Set("Overview");
            if (_applicationCache.TryGetValue(app.FullName, out var cached))
            {
                packageConfiguration.Set(cached.Configuration);
                inventory.Set(cached.Inventory);
                settings.Set(cached.Settings);
                footprint.Set(cached.Footprint);
                inspecting.Set(false);
                status.Set("Cached application details loaded. Reload resources to refresh.");
                if (ReferenceEquals(_inspectionCancellation, cancellation)) _inspectionCancellation = null;
                return;
            }
            inspecting.Set(true); status.Set($"Loading {app.Name}…");
            try
            {
                // Let selection and loading indicators render before starting inspection.
                await Task.Yield();
                if (token.IsCancellationRequested) return;
                var configurationTask = Task.Run(() => new PackageConfigurationReader().Read(app));
                var filesTask = Task.Run(() => new AppDataInspector().Inspect(app, token));
                var settingsTask = Task.Run(() => new PackageSettingsReader().Read(app.FamilyName));
                async Task PublishFiles()
                {
                    try
                    {
                        var result = await filesTask;
                        if (!result.Canceled && !token.IsCancellationRequested && request == _selectionVersion) inventory.Set(result);
                    }
                    // The combined inspection below reports errors; this task only publishes files early.
                    catch (Exception) { }
                }
                var publishFilesTask = PublishFiles();
                await Task.WhenAll(configurationTask, filesTask, settingsTask);
                await publishFilesTask;
                if (token.IsCancellationRequested || filesTask.Result.Canceled || request != _selectionVersion) return;
                packageConfiguration.Set(configurationTask.Result);
                inventory.Set(filesTask.Result);
                settings.Set(settingsTask.Result);
                _applicationCache[app.FullName] = new(configurationTask.Result, filesTask.Result, settingsTask.Result, []);
                status.Set("Disk inventory loaded. Select a file to preview supported text formats.");
            }
            catch (OperationCanceledException) { }
            catch (Exception ex) { if (request == _selectionVersion) status.Set($"Inspection failed: {ex.Message}"); }
            finally { if (ReferenceEquals(_inspectionCancellation, cancellation)) _inspectionCancellation = null; if (request == _selectionVersion) inspecting.Set(false); }
        }

        async void Measure()
        {
            if (busy.Value || inspecting.Value || actionBusy.Value || selected.Value is null) return;
            var app = selected.Value;
            using var cancellation = new CancellationTokenSource();
            _actionCancellation = cancellation;
            var token = cancellation.Token;
            actionBusy.Set(true);
            try
            {
                var result = await Task.Run(() => new FootprintScanner().Scan(app, token));
                if (token.IsCancellationRequested) return;
                footprint.Set(result.ToArray());
                if (_applicationCache.TryGetValue(app.FullName, out var cached))
                    _applicationCache[app.FullName] = cached with { Footprint = result.ToArray() };
                NotifyAction("Logical file bytes measured; shared dependencies and unknown data locations are excluded.");
            }
            catch (OperationCanceledException) { }
            catch (Exception ex) { if (!token.IsCancellationRequested) NotifyAction($"Measurement failed: {ex.Message}"); }
            finally { if (ReferenceEquals(_actionCancellation, cancellation)) { _actionCancellation = null; actionBusy.Set(false); } }
        }

        async void PreviewFile(AppDataFile item)
        {
            if (busy.Value || inspecting.Value) return;
            _actionCancellation?.Cancel();
            using var cancellation = new CancellationTokenSource();
            _actionCancellation = cancellation;
            var token = cancellation.Token;
            file.Set(item); view.Set(item.Area); preview.Set("Loading preview…"); actionBusy.Set(true);
            try
            {
                if (item.Area == "Settings" && Path.GetFileName(item.FullPath).Equals("settings.dat", StringComparison.OrdinalIgnoreCase)
                    && selected.Value is not null)
                {
                    var family = selected.Value.FamilyName;
                    var result = await Task.Run(() => new PackageSettingsReader().Read(family));
                    if (token.IsCancellationRequested) return;
                    settings.Set(result);
                    preview.Set("Decoded package values are displayed above, grouped by settings container. Windows reads the live ApplicationData store for this package; this is not an offline file parser.");
                }
                else { var result = await Task.Run(() => new AppDataInspector().Preview(item)); if (token.IsCancellationRequested) return; preview.Set(result); }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex) { if (!token.IsCancellationRequested) preview.Set($"Preview unavailable: {ex.Message}"); }
            finally { if (ReferenceEquals(_actionCancellation, cancellation)) { _actionCancellation = null; actionBusy.Set(false); } }
        }

        void OpenFolder(string path)
        {
            try
            {
                if (!Directory.Exists(path)) { NotifyAction("Folder is unavailable or does not exist."); return; }
                Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
            }
            catch (Exception ex) { NotifyAction($"Cannot open folder: {ex.Message}"); }
        }

        void OpenFile(AppDataFile item)
        {
            try
            {
                if (!File.Exists(item.FullPath)) { NotifyAction("File is unavailable or no longer exists."); return; }
                Process.Start(new ProcessStartInfo(item.FullPath) { UseShellExecute = true, Verb = "open" });
                NotifyAction($"Opened {item.RelativePath} with its default application.");
            }
            catch (Exception ex) { NotifyAction($"Cannot open file with its default application: {ex.Message}"); }
        }

        void OpenView(string next) { _actionCancellation?.Cancel(); _actionCancellation = null; actionBusy.Set(false); view.Set(next); file.Set(null); }
        void ReloadApplication()
        {
            _applicationCache.Clear();
            if (selected.Value is null || busy.Value) return;
            var currentView = view.Value;
            Select(selected.Value);
            view.Set(currentView);
        }
        void RecoverList()
        {
            busy.Set(false);
            ClearSelection();
            railAttempt.Set(railAttempt.Value + 1);
            detailAttempt.Set(detailAttempt.Value + 1);
            status.Set("Choose an application from the list.");
        }
        return FlexColumn(
            Component<ActionNotificationStack>().WithKey("notifications")
                .Margin(left: 12, top: 8, right: 12).Flex(shrink: 0),
            ExplorerLayoutTemplates.Render(compact.Value,
                ErrorBoundary(Component<ApplicationRail, ApplicationRailProps>(new(apps.Value, selected.Value, inventory.Value,
                    view.Value, busy.Value, Select, Unselect, Scan, PreviewFile, OpenFile, OpenView, inspecting.Value && inventory.Value is null, compact.Value)).WithKey($"application-rail-{railAttempt.Value}"),
                    error => Component<ErrorRecoveryPanel, ErrorRecoveryPanelProps>(new("Application list unavailable", error,
                        () => railAttempt.Set(railAttempt.Value + 1), RecoverList)).Width(400))
                    .WithKey("rail-boundary").IsVisible(Props.NavigationVisible).Flex(shrink: 0),
                ErrorBoundary(Component<ApplicationDetailPane, ApplicationDetailPaneProps>(new(selected.Value, footprint.Value,
                    inventory.Value, settings.Value, packageConfiguration.Value, file.Value, preview.Value,
                    view.Value, busy.Value || inspecting.Value || actionBusy.Value, status.Value, OpenView, Measure, OpenFolder, OpenFile, PreviewFile, inspecting.Value, compact.Value, ReloadApplication, Uninstalled, actionNotice.Value, actionNoticeVersion.Value))
                    .WithKey($"application-detail-{detailAttempt.Value}"),
                    error => Component<ErrorRecoveryPanel, ErrorRecoveryPanelProps>(new("Application details unavailable", error,
                        () => detailAttempt.Set(detailAttempt.Value + 1), RecoverList)))
                    .WithKey("detail-boundary").MinWidth(0).Flex(grow: 1, basis: 0)
            ).Flex(grow: 1, basis: 0)
        ).HAlign(HorizontalAlignment.Stretch).MinWidth(0).Set(control =>
        {
            if (control.Tag as string == "responsive-explorer") return;
            control.Tag = "responsive-explorer";
            XamlRoot? observedRoot = null;
            void UpdateLayout()
            {
                var next = (control.XamlRoot?.Size.Width ?? control.ActualWidth) < 1000;
                if (_compactLayout == next) return;
                _compactLayout = next;
                compact.Set(next);
            }
            void RootChanged(XamlRoot sender, XamlRootChangedEventArgs args) => UpdateLayout();
            control.Loaded += (_, _) =>
            {
                observedRoot = control.XamlRoot;
                if (observedRoot is not null) observedRoot.Changed += RootChanged;
                UpdateLayout();
            };
            control.Unloaded += (_, _) =>
            {
                if (observedRoot is not null) observedRoot.Changed -= RootChanged;
                observedRoot = null;
            };
        });
    }
}
