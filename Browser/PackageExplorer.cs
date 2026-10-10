// SPDX-License-Identifier: GPL-3.0-only
using System.Diagnostics;
using Microsoft.UI.Xaml;
using Microsoft.UI.Reactor;
using Microsoft.UI.Reactor.Core;
using WinApps.Models;
using WinApps.Services.Inventory;
using WinApps.Services.Storage;
using WinApps.Services.Settings;
using WinApps.Services.Packages;
using static Microsoft.UI.Reactor.Factories;

using static WinApps.Browser.BrowserUi;

namespace WinApps.Browser;

public sealed record PackageExplorerProps(bool NavigationVisible = true);

public sealed class PackageExplorer : Component<PackageExplorerProps>
{
    private int _selectionVersion;
    private bool _compactLayout;
    private (string Key, RunningApplication[] Snapshot)? _nextActivitySnapshot;
    private CancellationTokenSource? _inspectionCancellation;
    private CancellationTokenSource? _actionCancellation;
    private sealed record CachedApplication(PackageConfiguration? Configuration, AppDataInventory Inventory,
        AppSettingsSnapshot Settings, FolderFootprint[] Footprint);
    private readonly Dictionary<string, CachedApplication> _applicationCache = new(StringComparer.Ordinal);
    private sealed record SelectionState(InstalledApp? Selected = null, AppDataInventory? Inventory = null,
        AppSettingsSnapshot? Settings = null, PackageConfiguration? Configuration = null, AppDataFile? File = null,
        string Preview = "", string View = "Overview", bool Inspecting = false, bool ActionBusy = false,
        string Status = "Loading your application library…", FolderFootprint[]? Footprint = null, RunningApplication[]? InitialActivity = null);
    private static readonly SelectionState InitialSelection = new();
    // Hook values are render snapshots. Async completion must merge with the current
    // selection, rather than the snapshot captured before the user's click.
    private SelectionState _currentSelection = InitialSelection;
    private sealed class SelectionField<T>(Func<T> read, Action<T> write)
    {
        public T Value => read();
        public void Set(T value) => write(value);
    }
    private readonly struct ResumeOnUi(Microsoft.UI.Dispatching.DispatcherQueue dispatcher) : System.Runtime.CompilerServices.INotifyCompletion
    {
        public ResumeOnUi GetAwaiter() => this;
        public bool IsCompleted => dispatcher.HasThreadAccess;
        public void GetResult() { }
        public void OnCompleted(Action continuation) => dispatcher.TryEnqueue(() => continuation());
    }
    public override Element Render()
    {
        var ui = new ResumeOnUi(Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread());
        var apps = UseState(Array.Empty<InstalledApp>());
        var selection = UseState(InitialSelection);
        void PublishSelection(SelectionState next)
        {
            _currentSelection = next;
            selection.Set(next);
        }
        var selected = new SelectionField<InstalledApp?>(() => _currentSelection.Selected, value => PublishSelection(_currentSelection with { Selected = value }));
        var footprint = new SelectionField<FolderFootprint[]>(() => _currentSelection.Footprint ?? [], value => PublishSelection(_currentSelection with { Footprint = value }));
        var inventory = new SelectionField<AppDataInventory?>(() => _currentSelection.Inventory, value => PublishSelection(_currentSelection with { Inventory = value }));
        var settings = new SelectionField<AppSettingsSnapshot?>(() => _currentSelection.Settings, value => PublishSelection(_currentSelection with { Settings = value }));
        var packageConfiguration = new SelectionField<PackageConfiguration?>(() => _currentSelection.Configuration, value => PublishSelection(_currentSelection with { Configuration = value }));
        var file = new SelectionField<AppDataFile?>(() => _currentSelection.File, value => PublishSelection(_currentSelection with { File = value }));
        var preview = new SelectionField<string>(() => _currentSelection.Preview, value => PublishSelection(_currentSelection with { Preview = value }));
        var view = new SelectionField<string>(() => _currentSelection.View, value => PublishSelection(_currentSelection with { View = value }));
        var busy = UseState(false);
        var inspecting = new SelectionField<bool>(() => _currentSelection.Inspecting, value => PublishSelection(_currentSelection with { Inspecting = value }));
        var actionBusy = new SelectionField<bool>(() => _currentSelection.ActionBusy, value => PublishSelection(_currentSelection with { ActionBusy = value }));
        var status = new SelectionField<string>(() => _currentSelection.Status, value => PublishSelection(_currentSelection with { Status = value }));
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
            PublishSelection(new SelectionState(Status: "Application unselected. Choose an application from the list."));
        }

        void Unselect()
        {
            if (busy.Value) return;
            ClearSelection();
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
            WinApps.Services.Icons.ApplicationIcons.InvalidateDisplayIcons();
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
            var initialActivity = _nextActivitySnapshot?.Key == app.FullName ? _nextActivitySnapshot.Value.Snapshot : null;
            _nextActivitySnapshot = null;
            _inspectionCancellation?.Cancel();
            _actionCancellation?.Cancel();
            using var cancellation = new CancellationTokenSource();
            _inspectionCancellation = cancellation;
            var token = cancellation.Token;
            var request = ++_selectionVersion;
            if (_applicationCache.TryGetValue(app.FullName, out var cached))
            {
                PublishSelection(new SelectionState(app, cached.Inventory, cached.Settings, cached.Configuration,
                    Status: "Cached application details loaded. Reload resources to refresh.", Footprint: cached.Footprint, InitialActivity: initialActivity));
                if (ReferenceEquals(_inspectionCancellation, cancellation)) _inspectionCancellation = null;
                return;
            }
            PublishSelection(new SelectionState(app, Inspecting: true, Status: $"Loading {app.Name}…", InitialActivity: initialActivity));
            try
            {
                // Let selection and loading indicators render before starting inspection.
                await Task.Yield();
                await ui;
                if (token.IsCancellationRequested) return;
                var configurationTask = Task.Run(() => new PackageConfigurationReader().Read(app));
                var filesTask = Task.Run(() => new AppDataInspector().Inspect(app, token));
                var settingsTask = Task.Run(() => new PackageSettingsReader().Read(app.FamilyName));
                async Task PublishFiles()
                {
                    try
                    {
                        var result = await filesTask;
                        await ui;
                        if (!result.Canceled && !token.IsCancellationRequested && request == _selectionVersion) inventory.Set(result);
                    }
                    // The combined inspection below reports errors; this task only publishes files early.
                    catch (Exception) { }
                }
                var publishFilesTask = PublishFiles();
                await Task.WhenAll(configurationTask, filesTask, settingsTask);
                await publishFilesTask;
                await ui;
                if (token.IsCancellationRequested || filesTask.Result.Canceled || request != _selectionVersion) return;
                PublishSelection(_currentSelection with { Configuration = configurationTask.Result, Inventory = filesTask.Result,
                    Settings = settingsTask.Result, Inspecting = false, Status = "Application details loaded." });
                _applicationCache[app.FullName] = new(configurationTask.Result, filesTask.Result, settingsTask.Result, []);
            }
            catch (OperationCanceledException) { }
            catch (Exception ex) { await ui; if (request == _selectionVersion) status.Set($"Inspection failed: {ex.Message}"); }
            finally { await ui; if (ReferenceEquals(_inspectionCancellation, cancellation)) _inspectionCancellation = null; if (request == _selectionVersion && inspecting.Value) inspecting.Set(false); }
        }

        void SelectActivity(InstalledApp app, RunningApplication[] snapshot)
        {
            _nextActivitySnapshot = (app.FullName, snapshot);
            Select(app);
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
        return Grid([], [], [
            ExplorerLayoutTemplates.Render(compact.Value,
                ErrorBoundary(Component<ApplicationRail, ApplicationRailProps>(new(apps.Value, selected.Value, inventory.Value,
                    view.Value, busy.Value, Select, Unselect, Scan, PreviewFile, OpenFile, OpenView, inspecting.Value && inventory.Value is null, compact.Value)).WithKey($"application-rail-{railAttempt.Value}"),
                    error => Component<ErrorRecoveryPanel, ErrorRecoveryPanelProps>(new("Application list unavailable", error,
                        () => railAttempt.Set(railAttempt.Value + 1), RecoverList)).Width(400))
                    .WithKey("rail-boundary").IsVisible(Props.NavigationVisible).Flex(shrink: 0),
                ErrorBoundary(Component<ApplicationDetailPane, ApplicationDetailPaneProps>(new(selected.Value, footprint.Value,
                    inventory.Value, settings.Value, packageConfiguration.Value, file.Value, preview.Value,
                    view.Value, busy.Value || inspecting.Value || actionBusy.Value, status.Value, OpenView, Measure, OpenFolder, OpenFile, PreviewFile, inspecting.Value, compact.Value, ReloadApplication, Uninstalled, actionNotice.Value, actionNoticeVersion.Value, apps.Value, Select, Unselect, _currentSelection.InitialActivity, SelectActivity))
                    .WithKey($"application-detail-{detailAttempt.Value}"),
                    error => Component<ErrorRecoveryPanel, ErrorRecoveryPanelProps>(new("Application details unavailable", error,
                        () => detailAttempt.Set(detailAttempt.Value + 1), RecoverList)))
                    .WithKey("detail-boundary").MinWidth(0).Flex(grow: 1, basis: 0)
            ).HAlign(HorizontalAlignment.Stretch).VAlign(VerticalAlignment.Stretch),
            Component<StackBehaviorNotification>().WithKey("notifications")
                .HAlign(HorizontalAlignment.Right).VAlign(VerticalAlignment.Bottom)
                .Margin(20)
        ]).HAlign(HorizontalAlignment.Stretch).MinWidth(0).Set(control =>
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
