// SPDX-License-Identifier: GPL-3.0-only
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Reactor.Core;
using Microsoft.UI.Reactor;
using WinApps.Browser;
using WinApps.Models;
using static Microsoft.UI.Reactor.Factories;

namespace MyApps.UiIntegration;

internal static class Program
{
    [System.Runtime.InteropServices.DllImport("kernel32.dll")]
    private static extern uint SetErrorMode(uint mode);
    [STAThread]
    private static void Main()
    {
        // Test failures must report through stderr/logs without interrupting the user's desktop.
        SetErrorMode(0x0002); // SEM_NOGPFAULTERRORBOX
        Directory.CreateDirectory("artifacts");
        File.WriteAllText("artifacts/ui-checks.log", "Starting native UI checks.\n");
        try
        {
            WinRT.ComWrappersSupport.InitializeComWrappers();
            Application.Start(_ => new ChecksApplication());
        }
        catch (Exception error)
        {
            File.AppendAllText("artifacts/ui-checks.log", error.ToString());
            Environment.ExitCode = 1;
        }
    }
}

public sealed partial class ChecksApplication : Application
{
    private readonly Window _window;
    public ChecksApplication()
    {
        File.AppendAllText("artifacts/ui-checks.log", "Initializing resources.\n");
        InitializeComponent();
        // A hidden, unactivated window keeps the WinUI dispatcher alive for native checks.
        _window = new Window { Content = new Grid() };
        Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread().TryEnqueue(Run);
    }

    private async void Run()
    {
        try
        {
            if (Environment.GetCommandLineArgs().Contains("--compact-activity-checks"))
            {
                void Expect(bool condition, string message)
                {
                    if (!condition) throw new InvalidOperationException(message);
                }
                var fixture = new InstalledApp("Activity fixture", "", "activity-fixture", "", "1", "", "");
                var label = new TextBlock();
                var badge = new Border { Child = label };
                var tileButton = new Button { Content = badge };
                var status = new TextBlock();
                var indicator = new Border();
                var tile = new CompactActivityTile(fixture, tileButton, badge, label, status, indicator);
                var activityFirst = new WinApps.Services.Inventory.RunningApplication(123, 1, fixture.Name, "First window", "fixture.exe", 0, 1, WindowHandle: 1);
                var activitySecond = activityFirst with { Id = 124, Title = "Second window" };
                tile.Update([activityFirst, activitySecond]);
                Expect(label.Text == "2 processes" && badge.Visibility == Visibility.Visible, "Multiple processes must appear in the Mini badge.");
                Expect((ToolTipService.GetToolTip(badge) as string)?.Contains("PID 124") == true,
                    "Instance tooltip must include window titles and process IDs.");
                await new UiDelay(1800);
                Expect(indicator.Background is not null && status.Text.Contains("Running"),
                    "Running tiles must show a status indicator and text.");
                tile.Update([activityFirst with { Memory = 104857600 }]);
                Expect(status.Text.Contains("100 MiB RAM") && badge.Visibility == Visibility.Collapsed,
                    "Memory must update even when the app stays running; a single process needs no badge.");
                tile.Update([]);
                await new UiDelay(1800);
                Expect(badge.Visibility == Visibility.Collapsed, "The final closed instance must settle back to normal.");
                tile.Stop();
                tile.Update([activityFirst]);
                Expect(status.Text == "Suggested", "Stopped tiles must ignore late updates.");
                var companion = new Window { Content = new Grid() };
                var activity = new CompactAppActivity(companion, () => [], () => [tile]);
                activity.Start();
                activity.Dispose();
                companion.Close();
                activity.Refresh();
                await new UiDelay(2200);
                Console.WriteLine("Passed compact activity badge, tooltip, transition, and closed-window callback checks.");
                Environment.ExitCode = 0;
                return;
            }
            if (Environment.GetCommandLineArgs().Contains("--compact-search-checks"))
            {
                // Build and exercise the companion without activating either test window.
                var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
                var type = typeof(CompactSearchWindowController);
                var controller = Activator.CreateInstance(type, flags, null, [_window], null)!;
                type.GetMethod("EnsureWindow", flags)!.Invoke(controller, null);
                var companion = (Window)type.GetField("_searchWindow", flags)!.GetValue(controller)!;
                var content = (Grid)companion.Content;
                var titleBar = (TitleBar)content.FindName("CompactTitleBar");
                if (!companion.ExtendsContentIntoTitleBar || titleBar.RightHeader is not null || titleBar.LeftHeader is not Border { Child: Microsoft.UI.Xaml.Controls.Image })
                    throw new InvalidOperationException("Compact mode must extend its themed title bar, show the app icon without a duplicate return control.");
                var search = (TextBox)content.FindName("Search");
                var badges = (Grid)content.FindName("Badges");
                var status = (TextBlock)content.FindName("Status");
                var alpha = new InstalledApp("Alpha browser with a long wrapping name", "family.alpha", "alpha", "Publisher A", "", "", "");
                var beta = alpha with { Name = "Beta editor", FamilyName = "family.beta", FullName = "beta", Publisher = "Publisher B" };
                type.GetField("_apps", flags)!.SetValue(controller, new[] { alpha, beta });
                type.GetField("_mostViewed", flags)!.SetValue(controller, new[] { beta });
                type.GetField("_select", flags)!.SetValue(controller, (Action<InstalledApp>)(_ => { }));
                type.GetMethod("RefreshSuggestions", flags)!.Invoke(controller, null);
                if (!status.Text.StartsWith("Suggested for you") || badges.Children.Count != 1)
                    throw new InvalidOperationException("Empty search must show most-viewed app badges.");
                search.Text = "publisher a";
                type.GetMethod("RefreshSuggestions", flags)!.Invoke(controller, null);
                await new UiDelay(80);
                if (badges.Children.Count != 1 || !Microsoft.UI.Xaml.Automation.AutomationProperties.GetName(badges.Children[0]).Contains(alpha.Name))
                    throw new InvalidOperationException("Search must match publishers and identify the app badge.");
                if (((Button)badges.Children[0]).ContextFlyout is not Microsoft.UI.Xaml.Controls.MenuFlyout)
                    throw new InvalidOperationException("Compact suggestions must expose the shared app action menu.");
                var name = Descendants((UIElement)badges.Children[0]).OfType<TextBlock>().Single(text => text.Text == alpha.Name);
                if (name.TextWrapping != TextWrapping.Wrap || name.MaxLines != 0 || ((Button)badges.Children[0]).Padding.Left < 8)
                    throw new InvalidOperationException("Suggestion badges must wrap app names with padding.");
                search.Text = "no such app";
                type.GetMethod("RefreshSuggestions", flags)!.Invoke(controller, null);
                await new UiDelay(80);
                if (badges.Children.Count != 0 || status.Text != "No matching applications.")
                    throw new InvalidOperationException("Unmatched searches must clear stale suggestions.");
                search.Text = "family.beta";
                type.GetMethod("RefreshSuggestions", flags)!.Invoke(controller, null);
                await new UiDelay(80);
                if (badges.Children.Count != 1)
                    throw new InvalidOperationException("Search must match package family names.");
                if (companion.AppWindow.IsShownInSwitchers)
                    throw new InvalidOperationException("The companion must not create a second taskbar entry.");
                if (content.FindName("OpenFull") is not Microsoft.UI.Xaml.Controls.Button || content.FindName("QuitApp") is not null)
                    throw new InvalidOperationException("Compact view must offer one footer return action and use the window close button for quit.");
                type.GetField("_activeKeys", flags)!.SetValue(controller, new HashSet<string> { alpha.FullName });
                search.Text = "";
                type.GetMethod("RefreshSuggestions", flags)!.Invoke(controller, null);
                await new UiDelay(80);
                if (badges.Children.Count != 2 || !Microsoft.UI.Xaml.Automation.AutomationProperties.GetName(badges.Children[0]).Contains(alpha.Name))
                    throw new InvalidOperationException("Running installed apps outside the learned shortlist must appear before suggestions.");
                var overviewRequests = 0;
                type.GetField("_select", flags)!.SetValue(controller, (Action<InstalledApp>)(_ => overviewRequests++));
                type.GetMethod("Open", flags)!.Invoke(controller, [beta]);
                type.GetMethod("Open", flags)!.Invoke(controller, [beta]);
                if (overviewRequests != 1 || badges.Children.Count != 6 || !status.Text.StartsWith("Loading Beta") || search.IsEnabled ||
                    !Descendants((UIElement)badges.Children[0]).OfType<ProgressRing>().Any(ring => ring.IsActive))
                    throw new InvalidOperationException("Compact overview opening must show progress and block duplicate requests until ready.");
                type.GetMethod("MainClosed", flags)!.Invoke(controller, [_window, null]);
                type.GetMethod("MainClosed", flags)!.Invoke(controller, [_window, null]);
                if (type.GetField("_searchWindow", flags)!.GetValue(controller) is not null || badges.Children.Count != 0)
                    throw new InvalidOperationException("Shutdown must release companion window references and tile assets, and tolerate repeated cleanup.");
                if (type.GetField("_pendingOverview", flags)!.GetValue(controller) is not null)
                    throw new InvalidOperationException("Shutdown must clear pending overview requests.");
                var placementTimer = (DispatcherTimer)type.GetField("_placementTimer", flags)!.GetValue(controller)!;
                if (placementTimer.IsEnabled)
                    throw new InvalidOperationException("Shutdown must stop placement callbacks before window destruction.");
                search.Text = "late callback";
                await new UiDelay(2200);
                Console.WriteLine("Passed compact search native resource, filtering, wrapping, padding, and close checks.");
                File.AppendAllText("artifacts/ui-checks.log", "Passed compact search native checks.\n");
                return;
            }
            if (Environment.GetCommandLineArgs().Contains("--launch-diagnostics"))
            {
                var registered = new WinApps.Services.Inventory.DesktopAppCatalog().Scan();
                var shortcuts = new WinApps.Services.Inventory.StartMenuCatalog().Scan();
                var merged = WinApps.Services.Inventory.DesktopAppMerge.Merge(registered.Apps, shortcuts.Apps);
                foreach (var group in new[] { ("registered", registered.Apps), ("shortcuts", shortcuts.Apps), ("merged", merged) })
                    foreach (var item in group.Item2.Where(item => item.Name.Contains("CapCut", StringComparison.OrdinalIgnoreCase)))
                        Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(new { Stage = group.Item1, App = item, CanLaunch = WinApps.Services.Packages.ApplicationLauncher.CanLaunch(item) }));
                var target = merged.Single(item => item.Name == "CapCut");
                if (!WinApps.Services.Packages.ApplicationLauncher.CanLaunch(target)) throw new InvalidOperationException("CapCut has no launch target.");
                var launchReconciler = new Reconciler();
                var launchControls = launchReconciler.Mount(Component<ApplicationLaunchActions, ApplicationLaunchActionsProps>(new(target, null)), () => { })!;
                if (!Descendants(launchControls).OfType<Button>().Any(button =>
                    Microsoft.UI.Xaml.Automation.AutomationProperties.GetName(button) == "Launch CapCut" && button.IsEnabled))
                    throw new InvalidOperationException("CapCut header Launch button is missing or disabled.");
                Console.WriteLine("Verified enabled CapCut header Launch button.");
                if (Environment.GetCommandLineArgs().Contains("--launch-capcut"))
                {
                    var result = await new WinApps.Services.Packages.ApplicationLauncher().LaunchAsync(target, false);
                    Console.WriteLine(result);
                    if (result != "Application launched.") throw new InvalidOperationException(result);
                }
                Console.WriteLine("Verified CapCut launch target.");
                return;
            }
            if (Environment.GetCommandLineArgs().Contains("--close-checks"))
            {
                if (WinApps.Services.Inventory.ApplicationWindows.IsAppWindow(Windows.Win32.PInvoke.GetShellWindow()))
                    throw new InvalidOperationException("The desktop shell must never be a close target.");
                using var current = System.Diagnostics.Process.GetCurrentProcess();
                var self = new WinApps.Services.Inventory.RunningApplication(current.Id, current.StartTime.ToUniversalTime().Ticks,
                    "WinApps", "", current.MainModule!.FileName, 0, 0);
                if (!WinApps.Services.Inventory.RunningApplications.RequestClose(self).Contains("cannot close itself"))
                    throw new InvalidOperationException("Self-close protection failed.");
                Console.WriteLine("Passed desktop-shell and self-close protection checks.");
                return;
            }
            if (Environment.GetCommandLineArgs().Contains("--registry-process-check"))
            {
                var registryEditor = new WinApps.Services.Inventory.StartMenuCatalog().Scan().Apps.Single(app => app.Name == "Registry Editor");
                var matches = WinApps.Services.Inventory.RunningApplications.Read(registryEditor);
                if (matches.Any(item => !item.Path.Equals(registryEditor.ExecutablePath, StringComparison.OrdinalIgnoreCase)))
                    throw new InvalidOperationException("Registry Editor matched an unrelated executable.");
                Console.WriteLine($"Registry Editor executable: {registryEditor.ExecutablePath}; matching processes: {matches.Length}");
                foreach (var item in matches) Console.WriteLine($"PID {item.Id}: {item.Path}");
                var cache = new WinApps.Services.Inventory.ApplicationLibraryCache(Path.GetFullPath("artifacts/old-library-check.json"));
                System.IO.File.WriteAllText(Path.GetFullPath("artifacts/old-library-check.json"), "{\"SchemaVersion\":2,\"Apps\":[]}");
                if (cache.Load() is not null) throw new InvalidOperationException("Old inventory must trigger a new scan.");
                Console.WriteLine("Passed exact executable matching and cache migration checks.");
                return;
            }
            if (Environment.GetCommandLineArgs().Contains("--asset-checks"))
            {
                var folder = Path.GetFullPath(Path.Combine("artifacts", "asset-check-" + Guid.NewGuid().ToString("N")));
                Directory.CreateDirectory(folder);
                var source = Path.Combine(folder, "source.png");
                using (var bitmap = new System.Drawing.Bitmap(64, 64)) bitmap.Save(source, System.Drawing.Imaging.ImageFormat.Png);
                var fixture = new InstalledApp("Fixture", "", "fixture", "", "", "", "",
                    IconUri: new Uri(source).AbsoluteUri, ExecutablePath: Environment.ProcessPath,
                    LaunchArguments: "--fixture argument", WorkingDirectory: folder);
                var png = WinApps.Services.Packages.ApplicationAssets.ExportImage(fixture, false, folder);
                var ico = WinApps.Services.Packages.ApplicationAssets.ExportImage(fixture, true, folder);
                using (var icon = new System.Drawing.Icon(ico))
                    if (icon.Width != 64) throw new InvalidOperationException("Invalid exported ICO.");
                using (var image = new System.Drawing.Bitmap(png))
                    if (image.Width != 64) throw new InvalidOperationException("Invalid exported PNG.");
                var linkPath = WinApps.Services.Packages.ApplicationAssets.CreateShortcut(fixture, folder);
                dynamic shell = Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell")!)!;
                dynamic link = shell.CreateShortcut(linkPath);
                try
                {
                    if ((string)link.Arguments != fixture.LaunchArguments || (string)link.WorkingDirectory != folder ||
                        !string.Equals((string)link.TargetPath, fixture.ExecutablePath, StringComparison.OrdinalIgnoreCase))
                        throw new InvalidOperationException("Shortcut must preserve target, arguments and working directory.");
                }
                finally { System.Runtime.InteropServices.Marshal.FinalReleaseComObject(link); System.Runtime.InteropServices.Marshal.FinalReleaseComObject(shell); }
                var duplicate = WinApps.Services.Packages.ApplicationAssets.CreateShortcut(fixture with { LaunchPath = linkPath }, folder);
                if (duplicate == linkPath || !File.ReadAllBytes(linkPath).SequenceEqual(File.ReadAllBytes(duplicate)))
                    throw new InvalidOperationException("Existing shortcuts must copy exactly without overwriting.");
                var packagedShortcut = WinApps.Services.Packages.ApplicationAssets.CreateShortcut(fixture with { FamilyName = "fixture-family", FullName = "fixture-family!App" }, folder);
                dynamic packageShell = Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell")!)!;
                dynamic packageLink = packageShell.CreateShortcut(packagedShortcut);
                try
                {
                    var iconPath = ((string)packageLink.IconLocation).Split(',')[0];
                    if (!File.Exists(iconPath) || !iconPath.EndsWith(".ico", StringComparison.OrdinalIgnoreCase))
                        throw new InvalidOperationException("Packaged shortcuts must use application artwork instead of Explorer's icon.");
                }
                finally { System.Runtime.InteropServices.Marshal.FinalReleaseComObject(packageLink); System.Runtime.InteropServices.Marshal.FinalReleaseComObject(packageShell); }
                Console.WriteLine("Passed PNG, ICO and shortcut preservation checks.");
                return;
            }
            var reconciler = new Reconciler();
            var checks = 0;
            void Check(bool value, string message)
            {
                if (!value) throw new InvalidOperationException(message);
                checks++;
            }
            var placementPath = Path.GetFullPath(Path.Combine("artifacts", "window-placement-check-" + Guid.NewGuid().ToString("N") + ".json"));
            var placementStore = new WinApps.Services.Storage.WindowPlacementStore(placementPath);
            Check(placementStore.Save(new(-500, -500, 100, 100)) is null, "Placement fixture must save successfully.");
            WindowPlacementController.Attach(_window, placementStore);
            WindowPlacementController.Attach(_window, placementStore);
            var workArea = NativeMonitor.WorkArea(0, 0);
            Check(_window.AppWindow.Position.X >= 0 && _window.AppWindow.Position.Y >= 0,
                "Native window startup must correct negative saved coordinates without display enumeration.");
            _window.AppWindow.Resize(new Windows.Graphics.SizeInt32(100, 100));
            _window.AppWindow.Move(new Windows.Graphics.PointInt32(workArea.X + 8, workArea.Y + 8));
            await new UiDelay(500);
            Check(_window.AppWindow.Size.Width >= Math.Min(768, workArea.Width) &&
                _window.AppWindow.Size.Height >= Math.Min(960, workArea.Height),
                "Native window resizing must enforce the minimum dimensions that fit the screen.");
            var restoredPlacement = placementStore.Load();
            Check(restoredPlacement is { X: >= 0, Y: >= 0 } && restoredPlacement.Width == _window.AppWindow.Size.Width,
                "Native placement changes must persist validated bounds.");
            File.AppendAllText("artifacts/ui-checks.log", "Passed native window placement checks.\n");
            Console.WriteLine("Passed native window placement checks.");
            foreach (var requestedTheme in new[] { ElementTheme.Light, ElementTheme.Dark })
            {
                var settings = SettingsFlyout.Create(requestedTheme, requestedTheme == ElementTheme.Dark,
                    BackdropKind.Mica, () => { }, _ => { });
                var settingsPanel = (StackPanel)((ScrollViewer)settings.Content).Content;
                var about = (StackPanel)((Border)settingsPanel.Children.Last()).Child;
                var links = about.Children.OfType<StackPanel>().Single().Children.OfType<HyperlinkButton>().ToArray();
                Check(links.Select(link => link.Content.ToString()).SequenceEqual(new[] { "Features", "Privacy", "License" }),
                    "Settings must include Features, Privacy, and License in one bottom card.");
                Check(about.Children.OfType<TextBlock>().Any(text => text.Text.StartsWith("Version ")) &&
                    about.Children.OfType<TextBlock>().Any(text => text.Text == "John M. Doyle"),
                    "The settings card must show the running version and author.");
                var frame = WinAppsShell.WindowFrame(requestedTheme, TextBlock("Title"),
                    ErrorBoundary(TextBlock("Content"), _ => TextBlock("Recovery")));
                Check(frame.Modifiers?.Backdrop?.Kind == BackdropKind.MicaAlt,
                    "Mica Alt must be the default on the window root, outside the recovery boundary.");
                var frameNative = (FrameworkElement)reconciler.Mount(frame, () => { })!;
                Check(frameNative.RequestedTheme == requestedTheme,
                    "The window root must own the selected theme for all descendants.");
                var surfaces = frameNative.Resources.MergedDictionaries.Single(dictionary => dictionary.ThemeDictionaries.ContainsKey("HighContrast"));
                var themeSurfaces = (ResourceDictionary)surfaces.ThemeDictionaries[requestedTheme.ToString()];
                Check(((Microsoft.UI.Xaml.Media.SolidColorBrush)themeSurfaces["CardBackgroundFillColorDefaultBrush"]).Color.A < 128,
                    "Cards must use translucent root resources so the backdrop remains visible.");
                var contrastSurfaces = (ResourceDictionary)surfaces.ThemeDictionaries["HighContrast"];
                Check(((Microsoft.UI.Xaml.Media.SolidColorBrush)contrastSurfaces["CardBackgroundFillColorDefaultBrush"]).Color.A == 255,
                    "High-contrast cards must remain opaque.");
                foreach (var kind in new[] { BackdropKind.Mica, BackdropKind.DesktopAcrylic, BackdropKind.MicaAlt })
                {
                    var nextFrame = WinAppsShell.WindowFrame(requestedTheme, TextBlock("Title"), TextBlock("Content"), kind);
                    Check(nextFrame.Modifiers?.Backdrop?.Kind == kind, "The selected material must be set only on the root.");
                }
            }
            var app = new InstalledApp("Fixture", "fixture-family", "fixture-family!App", "Publisher", "1", "", "");
            if (Environment.GetCommandLineArgs().Contains("--tab-navigation-checks"))
            {
                var tabView = "Overview";
                Element TabDetail() => Component<ApplicationDetailPane, ApplicationDetailPaneProps>(new(app, [], null,
                    new([], []), null, null, "", tabView, false, "", next => tabView = next,
                    () => { }, _ => { }, _ => { }, _ => { })).WithKey("tab-detail");
                var tabTree = TabDetail();
                var tabNative = reconciler.Mount(tabTree, () => { })!;
                foreach (var nextView in new[] { "Settings", "Overview", "Registry", "Overview" })
                {
                    var button = Descendants(tabNative).OfType<Button>().Single(control =>
                        Microsoft.UI.Xaml.Automation.AutomationProperties.GetName(control) == $"View {nextView}");
                    ((Microsoft.UI.Xaml.Automation.Provider.IInvokeProvider)new Microsoft.UI.Xaml.Automation.Peers.ButtonAutomationPeer(button)
                        .GetPattern(Microsoft.UI.Xaml.Automation.Peers.PatternInterface.Invoke)).Invoke();
                    await new UiDelay(50);
                    Check(tabView == nextView, "Tab clicks must change the selected section.");
                    var nextTree = TabDetail();
                    tabNative = reconciler.Reconcile(tabTree, nextTree, tabNative, () => { })!;
                    tabTree = nextTree;
                    Check(Descendants(tabNative).OfType<Button>().Where(control =>
                        Microsoft.UI.Xaml.Automation.AutomationProperties.GetName(control).StartsWith("View ")).All(control => control.IsEnabled),
                        "Section tabs must remain enabled after native clicks and reconciliation.");
                }
                Console.WriteLine("Passed repeated native tab-navigation checks.");
                return;
            }
            var railApps = new[] { app };
            var hiddenWindow = new Window();
            var hiddenHandle = WinRT.Interop.WindowNative.GetWindowHandle(hiddenWindow);
            var windowFixture = new WinApps.Services.Inventory.RunningApplication(Environment.ProcessId, DateTime.UtcNow.Ticks,
                "Fixture", "Fixture window", "fixture.exe", 1024, 1, WindowHandle: hiddenHandle);
            Check(!WinApps.Services.Inventory.ApplicationWindowSwitcher.HasOpenWindow(windowFixture),
                "A hidden background window must not remain an open-window activity card.");
            hiddenWindow.Close();
            Check(!WinApps.Services.Inventory.ApplicationWindowSwitcher.HasOpenWindow(windowFixture),
                "A destroyed window must be removed even when its process still runs.");
            Check(!WinApps.Services.Inventory.ApplicationWindowSwitcher.HasOpenWindow(windowFixture with { WindowHandle = 0 }),
                "A process without a window must not remain an open-window activity card.");
            if (Environment.GetEnvironmentVariable("WINAPPS_UI_TEST_FOCUS") == "activity")
            {
                var existing = windowFixture with { Id = 101, Started = 1, Name = "Existing app", Memory = 1048576 };
                var added = windowFixture with { Id = 102, Started = 2, Name = "New app", Memory = 2097152 };
                var reads = 0;
                var closed = false;
                var summary = "";
                var activityProps = new ApplicationActivityPanelProps(OnSummary: value => summary = value,
                    ReadWindows: (excluded, token) =>
                    {
                        Interlocked.Increment(ref reads);
                        return excluded is null ? [existing] : !closed && !excluded.Contains(added.Id) ? [added] : [];
                    }, WindowOpen: item => item.Id != added.Id || !closed);
                Element Activity() => Component<ApplicationActivityPanel, ApplicationActivityPanelProps>(activityProps).WithKey("activity-test");
                var activityTree = Activity();
                UIElement? activityNative = null;
                void RenderActivity()
                {
                    if (activityNative is null) return;
                    var next = Activity();
                    activityNative = reconciler.Reconcile(activityTree, next, activityNative, RenderActivity)!;
                    activityTree = next;
                    _window.Content = activityNative;
                }
                activityNative = reconciler.Mount(activityTree, RenderActivity)!;
                _window.Content = activityNative;
                await new UiDelay(2600);
                var cards = Descendants(activityNative).OfType<Border>().Where(border =>
                    Microsoft.UI.Xaml.Automation.AutomationProperties.GetName(border).Contains("MiB RAM, PID")).ToArray();
                Check(cards.Length == 2 && Microsoft.UI.Xaml.Automation.AutomationProperties.GetName(cards[0]).StartsWith("New app,"),
                    $"A newly detected window must be inserted before existing cards. Cards={cards.Length}; reads={reads}; summary={summary}; names={string.Join(" | ", cards.Select(card => Microsoft.UI.Xaml.Automation.AutomationProperties.GetName(card)))}");
                Check(summary.Contains("2 windowed apps") && summary.Contains("3 MiB RAM snapshots"),
                    "A new card's initial RAM snapshot must be included in the total.");
                Check(cards[0].BorderThickness.Left == 2 && Descendants(activityNative).OfType<TextBlock>().Any(text => text.Text == "NEW · SNAPSHOT"),
                    $"New cards must show their highlighted border and new-snapshot label. Thickness={cards[0].BorderThickness.Left}; brush={cards[0].BorderBrush?.GetType().Name}; labels={string.Join(" | ", Descendants(activityNative).OfType<TextBlock>().Where(text => text.Text.Contains("SNAPSHOT")).Select(text => text.Text))}");
                var beforeAnimation = (cards[0].BorderBrush as Microsoft.UI.Xaml.Media.LinearGradientBrush)?.GradientStops[0].Color;
                await new UiDelay(220);
                if (new Windows.UI.ViewManagement.UISettings().AnimationsEnabled && beforeAnimation is not null)
                    Check((cards[0].BorderBrush as Microsoft.UI.Xaml.Media.LinearGradientBrush)?.GradientStops[0].Color != beforeAnimation,
                        "The rainbow border must animate without rebuilding the page.");
                BrowserMotion.AcknowledgeActivityList();
                await new UiDelay(50);
                Check(!Descendants(activityNative).OfType<TextBlock>().Any(text => text.Text == "NEW · SNAPSHOT"),
                    "Interacting with the application list must acknowledge new cards.");
                closed = true;
                await new UiDelay(2300);
                Check(summary.Contains("1 windowed apps") && !Descendants(activityNative).OfType<TextBlock>().Any(text => text.Text == "New app"),
                    "Closing the new window must remove its card and RAM snapshot from the total.");
                _window.Content = reconciler.Reconcile(activityTree, TextBlock("Left homepage"), activityNative, () => { });
                var readsAfterLeave = reads;
                await new UiDelay(2200);
                Check(reads == readsAfterLeave, "Leaving the homepage must stop window discovery.");
                Console.WriteLine("Passed new-window discovery, RAM totals, rainbow acknowledgement, closing, and cleanup checks.");
                Environment.ExitCode = 0;
                return;
            }
            using (var canceledStats = new CancellationTokenSource())
            {
                canceledStats.Cancel();
                Check(WinApps.Services.Inventory.RunningApplications.Read(app, false, canceledStats.Token).Length == 0,
                    "Canceling live statistics must return quietly without throwing.");
            }
            var statRenders = 0;
            var initialUsage = windowFixture with { Memory = 2097152, Threads = 3 };
            var statTree = Component<ApplicationResourceCard, ApplicationResourceCardProps>(new(app, [initialUsage]));
            var statNative = reconciler.Mount(statTree, () => statRenders++)!;
            Check(Descendants(statNative).OfType<TextBlock>().Any(text => text.Text == "2.0 MiB"),
                "The overview must display its carried RAM snapshot immediately, before its first live read.");
            await new UiDelay(5200);
            Check(statRenders == 0, "Live statistics must update native labels without requesting page reconciliation.");
            var overviewTree = Component<ApplicationOverviewCards, ApplicationOverviewCardsProps>(
                new(app, null, false, false, _ => { }, _ => { }));
            var overviewNative = reconciler.Mount(overviewTree, () => { })!;
            var originalDetails = Descendants(overviewNative).OfType<TextBox>().ToArray();
            foreach (var section in new[] { "Files", "Package", "Application", "Files" })
            {
                var button = Descendants(overviewNative).OfType<Button>().Single(control =>
                    Microsoft.UI.Xaml.Automation.AutomationProperties.GetName(control) == $"Show {section} details");
                var peer = new Microsoft.UI.Xaml.Automation.Peers.ButtonAutomationPeer(button);
                ((Microsoft.UI.Xaml.Automation.Provider.IInvokeProvider)peer.GetPattern(
                    Microsoft.UI.Xaml.Automation.Peers.PatternInterface.Invoke)).Invoke();
                await new UiDelay(50);
                Check(Microsoft.UI.Xaml.Automation.AutomationProperties.GetItemStatus(button) == "Selected",
                    "Overview cards must update selection promptly through native controls.");
                Check(originalDetails.SequenceEqual(Descendants(overviewNative).OfType<TextBox>()),
                    "Switching overview cards must retain all existing detail controls.");
                Check(button.BorderThickness.Left == 2 && button.RenderTransform is Microsoft.UI.Xaml.Media.ScaleTransform scale && Math.Abs(scale.ScaleX - 0.98) < 0.0001,
                    $"Selected cards must show an immediate inset border and scale. Border={button.BorderThickness.Left}; transform={button.RenderTransform?.GetType().Name}; scale={(button.RenderTransform as Microsoft.UI.Xaml.Media.ScaleTransform)?.ScaleX}");
            }
            Console.WriteLine("Passed native overview card selection and retained-control checks.");
            if (Environment.GetEnvironmentVariable("WINAPPS_UI_TEST_FOCUS") == "overview")
            {
                Environment.ExitCode = 0;
                return;
            }
            var backActions = 0;
            var loadingDetails = Component<ApplicationDetailPane, ApplicationDetailPaneProps>(new(app, [], null, null, null,
                null, "", "Overview", true, "Loading", _ => { }, () => { }, _ => { }, _ => { }, _ => { }, true,
                OnClearSelection: () => backActions++));
            var loadingNative = reconciler.Mount(loadingDetails, () => { })!;
            Check(Descendants(loadingNative).OfType<ProgressRing>().Single(ring => ring.IsActive).IsActive,
                "App inspection must show an active loading indicator in the detail pane.");
            var backButton = Descendants(loadingNative).OfType<Button>().Single(button =>
                Microsoft.UI.Xaml.Automation.AutomationProperties.GetName(button) == "Back to applications");
            ((Microsoft.UI.Xaml.Automation.Provider.IInvokeProvider)new Microsoft.UI.Xaml.Automation.Peers.ButtonAutomationPeer(backButton)
                .GetPattern(Microsoft.UI.Xaml.Automation.Peers.PatternInterface.Invoke)).Invoke();
            Check(!backButton.IsEnabled && backButton.Content is ProgressRing { IsActive: true },
                "Pressing Back must immediately show progress and prevent repeated navigation.");
            await new UiDelay(50);
            Check(backActions == 1, "The app header back button must clear selection even while inspection is loading.");
            Check(Descendants(loadingNative).OfType<Button>()
                .Where(button => new[] { "View Overview", "View Package", "View Storage", "View Settings", "View Registry", "View LocalState" }.Contains(Microsoft.UI.Xaml.Automation.AutomationProperties.GetName(button)))
                .All(button => !button.IsEnabled),
                "Section tabs must pause while app inspection runs so competing detail loads cannot overlap.");
            Check(Descendants(loadingNative).OfType<Button>().Single(button =>
                    Microsoft.UI.Xaml.Automation.AutomationProperties.GetName(button) == "Launch Fixture").IsEnabled,
                "Launching an app must remain available while its details are being inspected.");
            var railInventory = new AppDataInventory([], []);
            InstalledApp? railSelection = app;
            var unexpectedClears = 0;
            Element Rail() => Component<ApplicationRail, ApplicationRailProps>(new(railApps, railSelection, railInventory,
                "Package", false, _ => { }, () => unexpectedClears++, () => { }, _ => { }, _ => { }, _ => { })).WithKey("rail");
            var railTree = Rail();
            var railNative = reconciler.Mount(railTree, () => { })!;
            var railList = Descendants(railNative).OfType<ListView>().Single();
            var railItem = railList.Items[0];
            foreach (var mode in new[] { "Grid", "Rows", "Widgets" })
            {
                var nextRail = Rail();
                railNative = reconciler.Reconcile(railTree, nextRail, railNative, () => { })!;
                Check(ReferenceEquals(railList, Descendants(railNative).OfType<ListView>().Single()) &&
                    ReferenceEquals(railItem, railList.Items[0]),
                    $"Unrelated {mode} rendering must retain the rail's keyed item.");
                railTree = nextRail;
            }
            foreach (var nextSelection in new InstalledApp?[] { null, app, null, app })
            {
                railSelection = nextSelection;
                var nextRail = Rail();
                railNative = reconciler.Reconcile(railTree, nextRail, railNative, () => { })!;
                railTree = nextRail;
                await new UiDelay(50);
                Check(railSelection is not null && railList.SelectedIndex == Array.FindIndex(railApps, item => item.FullName == railSelection.FullName)
                    || railSelection is null && railList.SelectedIndex == -1,
                    $"The rail must follow the selected app by identity. Item type: {railList.Items[0]?.GetType().FullName}; selected: {railList.SelectedItem}");
                Check(unexpectedClears == 0, "Programmatic selection changes must not fire a clear-selection action.");
            }
            if (Environment.GetEnvironmentVariable("WINAPPS_UI_TEST_FOCUS") == "rail")
            {
                Console.WriteLine("Passed selection persistence and rail reconciliation checks.");
                Environment.ExitCode = 0;
                return;
            }
            if (Environment.GetEnvironmentVariable("WINAPPS_UI_TEST_FOCUS") == "rail-scroll")
            {
                var manyApps = Enumerable.Range(0, 60).Select(index => app with { Name = $"Fixture {index:D2}", FullName = $"fixture-{index}" }).ToArray();
                InstalledApp? chosen = null;
                AppDataInventory? scrollInventory = null;
                Element ScrollingRail() => Component<ApplicationRail, ApplicationRailProps>(new(manyApps, chosen, scrollInventory,
                    "Overview", false, _ => { }, () => { }, () => { }, _ => { }, _ => { }, _ => { })).WithKey("scrolling-rail");
                var scrollTree = ScrollingRail();
                var scrollNative = reconciler.Mount(scrollTree, () => { })!;
                _window.Content = scrollNative;
                await new UiDelay(200);
                chosen = manyApps[58];
                var nextScrollTree = ScrollingRail();
                scrollNative = reconciler.Reconcile(scrollTree, nextScrollTree, scrollNative, () => { })!;
                await new UiDelay(500);
                var nativeList = Descendants(scrollNative).OfType<ListView>().Single();
                var outerScroll = Descendants(scrollNative).OfType<ScrollView>().Single(control =>
                    Microsoft.UI.Xaml.Automation.AutomationProperties.GetName(control) == "Application rail scroll area");
                Check(nativeList.SelectedIndex == 58, "Homepage selection must highlight the app by key.");
                Check(outerScroll.VerticalOffset > 0, "Selection near the end must scroll the outer rail, not just its disabled inner scroller.");
                var selectedRow = (FrameworkElement)nativeList.ContainerFromIndex(58);
                var rowPosition = selectedRow.TransformToVisual(outerScroll).TransformPoint(new Windows.Foundation.Point(0, 0));
                Check(rowPosition.Y >= -1 && rowPosition.Y < outerScroll.ViewportHeight,
                    $"The selected row must be visible in the rail. Y={rowPosition.Y}; viewport={outerScroll.ViewportHeight}");
                scrollInventory = new AppDataInventory([], []);
                var expandedTree = ScrollingRail();
                scrollNative = reconciler.Reconcile(nextScrollTree, expandedTree, scrollNative, () => { })!;
                await new UiDelay(500);
                // Later inspection updates must not send a bring-into-view request
                // that positions the bottom of the expanded row above its app name.
                for (var update = 0; update < 3; update++)
                {
                    var refreshTree = ScrollingRail();
                    scrollNative = reconciler.Reconcile(expandedTree, refreshTree, scrollNative, () => { })!;
                    expandedTree = refreshTree;
                    await new UiDelay(100);
                }
                rowPosition = selectedRow.TransformToVisual(outerScroll).TransformPoint(new Windows.Foundation.Point(0, 0));
                Check(rowPosition.Y >= -1 && rowPosition.Y + Math.Min(selectedRow.ActualHeight, 300) <= outerScroll.ViewportHeight + 1,
                    $"The app name and files must remain visible after expansion and later updates. Y={rowPosition.Y}; height={selectedRow.ActualHeight}; viewport={outerScroll.ViewportHeight}");
                var anchoredOffset = outerScroll.VerticalOffset;
                outerScroll.ScrollTo(0, Math.Max(0, anchoredOffset - 120), new ScrollingScrollOptions(
                    ScrollingAnimationMode.Disabled, ScrollingSnapPointsMode.Ignore));
                await new UiDelay(300);
                Check(Math.Abs(outerScroll.VerticalOffset - anchoredOffset) < 1,
                    "An automatic offset change during selection must not displace the selected app and its files.");
                Console.WriteLine("Passed selected-row highlight and outer rail scrolling checks.");
                Environment.ExitCode = 0;
                return;
            }
            if (Environment.GetEnvironmentVariable("WINAPPS_UI_TEST_FOCUS") == "navigation")
            {
                SynchronizationContext.SetSynchronizationContext(new TestDispatcherContext(Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread()));
                var explorerTree = Component<PackageExplorer, PackageExplorerProps>(new());
                UIElement? explorerNative = null;
                void UpdateExplorer()
                {
                    if (explorerNative is not null)
                        explorerNative = reconciler.Reconcile(explorerTree, explorerTree, explorerNative, UpdateExplorer)!;
                }
                explorerNative = reconciler.Mount(explorerTree, UpdateExplorer)!;
                for (var attempt = 0; attempt < 100 && !Descendants(explorerNative).OfType<ListView>().Any(list => list.Items.Count > 0 && list.IsEnabled); attempt++)
                    await new UiDelay(100);
                var explorerList = Descendants(explorerNative).OfType<ListView>().Single(list => list.Items.Count > 0);
                // Invoke the same user-selection delegate that native item clicks call.
                var listTable = typeof(ApplicationRail).GetField("Lists", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!.GetValue(null)!;
                object?[] lookup = [explorerList, null];
                Check((bool)listTable.GetType().GetMethod("TryGetValue")!.Invoke(listTable, lookup)!, "The rail must attach native user-selection actions.");
                var listActions = lookup[1]!;
                InstalledApp[] ListedApps() => (InstalledApp[])listActions.GetType().GetField("Apps")!.GetValue(listActions)!;
                void SelectRow(int index) => ((Action<int>)listActions.GetType().GetField("Select")!.GetValue(listActions)!)(index);
                var target = ListedApps()[0];
                SelectRow(0);
                await new UiDelay(50);
                Check(explorerList.SelectedIndex >= 0 && ListedApps()[explorerList.SelectedIndex].FullName == target.FullName,
                    "The first selection must immediately synchronize the rail by app identity.");
                await new UiDelay(3000);
                Check(explorerList.SelectedIndex >= 0 && ListedApps()[explorerList.SelectedIndex].FullName == target.FullName,
                    "Asynchronous inspection completion must retain the first selection.");
                Check(!Descendants(explorerNative).OfType<TextBlock>().Any(text => text.Text == "On your PC now"),
                    "Inspection completion must not bounce back to the homepage.");
                var nextApp = ListedApps().First(item => item.FullName != target.FullName);
                SelectRow(Array.FindIndex(ListedApps(), item => item.FullName == nextApp.FullName));
                await new UiDelay(50);
                Check(ListedApps()[explorerList.SelectedIndex].FullName == nextApp.FullName, "Switching apps must publish the new selection immediately.");
                await new UiDelay(3000);
                Check(ListedApps()[explorerList.SelectedIndex].FullName == nextApp.FullName, "An older completion must not restore the previous app.");
                void ClearExplorer()
                {
                    var clear = Descendants(explorerNative).OfType<Button>().Single(button =>
                        Descendants(button).OfType<TextBlock>().Any(text => text.Text == "Clear selection"));
                    ((Microsoft.UI.Xaml.Automation.Provider.IInvokeProvider)new Microsoft.UI.Xaml.Automation.Peers.ButtonAutomationPeer(clear)
                        .GetPattern(Microsoft.UI.Xaml.Automation.Peers.PatternInterface.Invoke)).Invoke();
                }
                ClearExplorer();
                await new UiDelay(100);
                Check(explorerList.SelectedIndex == -1 && Descendants(explorerNative).OfType<TextBlock>().Any(text => text.Text == "Startup apps · at sign-in"),
                    "Clearing must immediately show the homepage and remove the rail highlight, before its activity snapshot finishes.");
                for (var attempt = 0; attempt < 50 && !Descendants(explorerNative).OfType<Button>().Any(button =>
                    Microsoft.UI.Xaml.Automation.AutomationProperties.GetName(button).EndsWith("overview in WinApps")); attempt++)
                    await new UiDelay(100);
                var homepageOpen = Descendants(explorerNative).OfType<Button>().FirstOrDefault(button =>
                    Microsoft.UI.Xaml.Automation.AutomationProperties.GetName(button).EndsWith("overview in WinApps"));
                if (homepageOpen is null)
                {
                    // Some test hosts cannot enumerate another process's window. Supply
                    // one deterministic activity card, wired to the explorer's real selector.
                    var activityFixture = Component<ApplicationActivityPanel, ApplicationActivityPanelProps>(new(Apps: [target],
                        OnSelect: chosen => SelectRow(Array.FindIndex(ListedApps(), item => item.FullName == chosen.FullName)),
                        Snapshot: [new WinApps.Services.Inventory.RunningApplication(1, DateTime.UtcNow.Ticks, target.Name, "Fixture window",
                            Path.Combine(target.InstallPath, "fixture.exe"), 1048576, 1)]));
                    var activityNative = reconciler.Mount(activityFixture, () => { })!;
                    homepageOpen = Descendants(activityNative).OfType<Button>().Single(button =>
                        Microsoft.UI.Xaml.Automation.AutomationProperties.GetName(button).EndsWith("overview in WinApps"));
                }
                ((Microsoft.UI.Xaml.Automation.Provider.IInvokeProvider)new Microsoft.UI.Xaml.Automation.Peers.ButtonAutomationPeer(homepageOpen!)
                    .GetPattern(Microsoft.UI.Xaml.Automation.Peers.PatternInterface.Invoke)).Invoke();
                await new UiDelay(100);
                Check(explorerList.SelectedIndex >= 0, "A single homepage action must select its app in the rail.");
                var homepageKey = ListedApps()[explorerList.SelectedIndex].FullName;
                await new UiDelay(3000);
                Check(explorerList.SelectedIndex >= 0 && ListedApps()[explorerList.SelectedIndex].FullName == homepageKey
                    && !Descendants(explorerNative).OfType<TextBlock>().Any(text => text.Text == "On your PC now"),
                    "A homepage selection must survive its asynchronous completion without a second click.");
                Console.WriteLine("Passed first-click selection, async completion, and app-switch navigation checks.");
                Environment.ExitCode = 0;
                return;
            }
            var resourceJson = System.Text.Json.JsonSerializer.Serialize(UiResources.Current);
            var resourceCopy = UiResources.Parse(resourceJson);
            Check(resourceCopy.Sections.ContainsKey("Windows package registration") && resourceCopy.Controls.Count == 3,
                "Bundled resources must include guides and all layout controls.");
            var invalidResourcesRejected = false;
            try { UiResources.Parse(resourceJson.Replace("https://", "http://")); }
            catch (InvalidDataException) { invalidResourcesRejected = true; }
            Check(invalidResourcesRejected, "Invalid resource links must be rejected before applying edits.");
            using (var paddedIcon = new System.Drawing.Bitmap(150, 150))
            {
                using (var paint = System.Drawing.Graphics.FromImage(paddedIcon))
                    paint.FillRectangle(System.Drawing.Brushes.Blue, 55, 65, 40, 20);
                using var normalizedIcon = WinApps.Services.Icons.ApplicationIcons.NormalizePackageIcon(paddedIcon);
                Check(normalizedIcon.Width == 64 && normalizedIcon.Height == 64,
                    "Normalized package icons must retain a consistent canvas.");
                Check(normalizedIcon.GetPixel(5, 32).A > 200 && normalizedIcon.GetPixel(32, 5).A == 0,
                    "Padded rectangular artwork must enlarge without losing its aspect ratio.");
                Check(normalizedIcon.GetPixel(0, 32).A == 0,
                    "Normalized artwork must retain an outer margin.");
            }
            using (var emptyIcon = new System.Drawing.Bitmap(32, 32))
            using (var normalizedEmpty = WinApps.Services.Icons.ApplicationIcons.NormalizePackageIcon(emptyIcon))
                Check(normalizedEmpty.GetPixel(32, 32).A == 0,
                    "Empty icons must remain transparent without invalid cropping.");
            Console.WriteLine("Passed package icon normalization checks.");
            if (Environment.GetCommandLineArgs().Contains("--action-notices"))
            {
                var noticeText = "First action completed";
                Element Notice() => VStack(0,
                    Component<StackBehaviorNotification>().WithKey("stack"),
                    Component<TransientActionNotice, TransientActionNoticeProps>(new(noticeText)).WithKey("notice"));
                var noticeTree = Notice();
                UIElement? noticeNative = null;
                void RenderNotice()
                {
                    if (noticeNative is null) return;
                    var next = Notice();
                    noticeNative = reconciler.Reconcile(noticeTree, next, noticeNative, RenderNotice)!;
                    noticeTree = next;
                }
                noticeNative = reconciler.Mount(noticeTree, RenderNotice)!;
                RenderNotice();
                await new UiDelay(2500);
                var noticeRoot = noticeNative;
                var noticeControl = Descendants(noticeNative).OfType<InfoBar>().Single(bar => bar.Message == noticeText);
                Check(noticeControl.Visibility == Visibility.Visible, "Action notices must initially be visible.");
                var noticeCard = Descendants(noticeNative).OfType<InfoBar>().Single(bar => bar.Message == noticeText);
                Check(noticeCard.IsOpen && noticeCard.MaxWidth == 400 && noticeCard.Severity == InfoBarSeverity.Success,
                    "Action notices must use compact semantic alerts.");
                Check(noticeCard.Title == "Action completed" && noticeCard.Content is null && noticeCard.CornerRadius.TopLeft == 16,
                    "Action notices must have rounded corners and no branding content.");
                noticeText = "Second action completed";
                RenderNotice();
                await new UiDelay(100);
                Check(Descendants(noticeNative).OfType<InfoBar>().Count(bar => bar.Message.EndsWith("action completed")) == 2,
                    "Notifications must stack instead of replacing the previous app's message.");
                await new UiDelay(5600);
                Check(Descendants(noticeNative).OfType<InfoBar>().Any(bar => bar.Message == noticeText) &&
                    !Descendants(noticeNative).OfType<InfoBar>().Any(bar => bar.Message == "First action completed"),
                    "An earlier timer must not dismiss a newer action notice.");
                await new UiDelay(3000);
                Check(!Descendants(noticeNative).OfType<InfoBar>().Any(bar => bar.Message.EndsWith("action completed")),
                    "Action notices must clear after eight seconds.");
                Check(ReferenceEquals(noticeRoot, noticeNative), "Notice expiry must retain its native root.");
                var uninstallCallbacks = 0;
                var actionTree = Component<ApplicationLaunchActions, ApplicationLaunchActionsProps>(new(app, null, _ => uninstallCallbacks++));
                var actionNative = reconciler.Mount(actionTree, () => { })!;
                Check(uninstallCallbacks == 0 && !Descendants(actionNative).OfType<Button>().Any(button => button.Content as string == "Confirm uninstall"),
                    "Mounting actions must neither uninstall an app nor show inline confirmation controls.");
                var desktopResult = new WinApps.Services.Packages.ApplicationUninstaller().UninstallAsync(app with { FamilyName = "" }).GetAwaiter().GetResult();
                Check(!desktopResult.Removed, "A desktop shortcut must not report package removal success.");
                Console.WriteLine("Passed focused stacked notification and uninstall checks.");
                Environment.ExitCode = 0;
                return;
            }
            Check(ApplicationManagementPanel.ManagementUri(app).OriginalString == "ms-settings:appsfeatures-app?fixture-family",
                "App management must target the package family, not the AUMID.");
            Check(ApplicationManagementPanel.ManagementUri(app with { FamilyName = "48996JohnM.Doyle.WebDive_jw9ac53qrz6qt" }).OriginalString
                == "ms-settings:appsfeatures-app?48996JohnM.Doyle.WebDive_jw9ac53qrz6qt",
                "Web Dive Advanced options must receive its registered package family.");
            Check(ApplicationManagementPanel.ManagementUri(app with { FamilyName = "", Source = "Desktop · Start Menu" }).OriginalString
                == "ms-settings:appsfeatures", "Shortcuts without a package identity must use the Installed apps fallback.");
            var launchedUris = new List<string>();
            Check(ApplicationManagementPanel.OpenManagementAsync(app, uri =>
            {
                launchedUris.Add(uri.OriginalString);
                return Task.FromResult(launchedUris.Count == 2);
            }).GetAwaiter().GetResult() && launchedUris.SequenceEqual(new[] {
                "ms-settings:appsfeatures-app?fixture-family", "ms-settings:appsfeatures" }),
                "A rejected direct launch must fall back to Installed apps.");
            launchedUris.Clear();
            Check(ApplicationManagementPanel.OpenManagementAsync(app, uri =>
            {
                launchedUris.Add(uri.OriginalString);
                return Task.FromResult(true);
            }).GetAwaiter().GetResult() && launchedUris.Count == 1,
                "An accepted direct launch must not also open the fallback.");
            var motionPanel = new StackPanel();
            var railSlot = (StackPanel)reconciler.Mount(BrowserUi.Slot("app-files", TextBlock("Files")), () => { })!;
            Check(railSlot.ChildrenTransitions is null || railSlot.ChildrenTransitions.Count == 0,
                "Shared rail slots must not replay detail entrance animations.");
            BrowserMotion.Enable(motionPanel);
            var transitions = motionPanel.ChildrenTransitions;
            BrowserMotion.Enable(motionPanel);
            Check(ReferenceEquals(transitions, motionPanel.ChildrenTransitions) && transitions.Count == 2,
                "Motion attachment must be idempotent across renders.");
            var markdownNative = reconciler.Mount(MarkdownContent.Render(
                "## Guide\n**Identity**\n[Microsoft Learn](https://learn.microsoft.com/en-us/windows/apps/)\n```xml\n<Package />\n```", "Guide"), () => { })!;
            Check(Descendants(markdownNative).OfType<TextBlock>().SelectMany(control => control.Inlines)
                .OfType<Microsoft.UI.Xaml.Documents.Hyperlink>().Single().NavigateUri.Host == "learn.microsoft.com",
                "Authored Markdown links must remain clickable native hyperlinks.");
            Check(Descendants(markdownNative).OfType<TextBox>().Single().Text == "<Package />",
                "Fenced XML must render as copyable code.");
            var codeNative = reconciler.Mount(MarkdownContent.Code("[link](https://example.com)\n```", "xml", "Raw"), () => { })!;
            Check(!Descendants(codeNative).OfType<TextBlock>().SelectMany(control => control.Inlines)
                .OfType<Microsoft.UI.Xaml.Documents.Hyperlink>().Any(), "Package source must not become an actionable Markdown link.");
            var properties = PackagePropertyGroups.Properties("<Applications><Application Id=\"One\"/><Application Id=\"Two\"/></Applications>");
            Check(properties.Count == 2 && properties[0].Key != properties[1].Key && properties[1].Value == "Two",
                "Property layouts must preserve distinct repeated XML declarations.");
            properties = PackagePropertyGroups.Properties("Location: C:\\Apps\\Fixture\nStatus: OK");
            Check(properties[0].Value == "C:\\Apps\\Fixture", "Property rows must preserve colons in values.");
            Element PropertyGroups() => Component<PackagePropertyGroups, PackagePropertyGroupsProps>(new([
                new("Windows package registration", "Name: Fixture\nVersion: 1"),
                new("Capabilities and permissions", "<Capabilities><Capability Name=\"internetClient\" /></Capabilities>")]))
                .WithKey("properties");
            var propertyTree = PropertyGroups();
            var propertyNative = reconciler.Mount(propertyTree, () => { })!;
            foreach (var mode in new[] { "Grid", "Widgets", "Rows", "Grid" })
            {
                var toggle = Descendants(propertyNative).OfType<Microsoft.UI.Xaml.Controls.Primitives.ToggleButton>()
                    .Single(control => Microsoft.UI.Xaml.Automation.AutomationProperties.GetName(control) == $"Package layout {mode}");
                toggle.IsChecked = true;
                var next = PropertyGroups();
                propertyNative = reconciler.Reconcile(propertyTree, next, propertyNative, () => { })!;
                Check(Descendants(propertyNative).OfType<Microsoft.UI.Xaml.Controls.Primitives.ToggleButton>()
                    .Single(control => Microsoft.UI.Xaml.Automation.AutomationProperties.GetName(control) == $"Package layout {mode}").IsChecked == true,
                    "The selected package layout must survive reconciliation.");
                propertyTree = next;
            }
            Element Launch(bool elevated) => Component<ApplicationLaunchActions, ApplicationLaunchActionsProps>(new(app,
                new("", "", "", "", [], "", [], Launch: new("App", "fixture.exe", elevated)))).WithKey("launch");
            var old = Launch(false);
            var native = reconciler.Mount(old, () => { })!;
            Check(Descendants(native).OfType<Button>().Count(button =>
                Microsoft.UI.Xaml.Automation.AutomationProperties.GetName(button) == "Uninstall Fixture") == 1,
                "Uninstall must be available beside Launch.");
            Check(!Descendants(native).OfType<Button>().Any(button => button.Content as string == "Confirm uninstall"),
                "Uninstall confirmation must use a modal dialog rather than inline controls.");
            Check(new WinApps.Services.Packages.ApplicationUninstaller().UninstallAsync(app with { FamilyName = "" })
                .GetAwaiter().GetResult().Message.Contains("Windows Installed apps"),
                "Desktop shortcuts must never be sent to package removal.");

            foreach (var enabled in new[] { true, false, true })
            {
                var next = Launch(enabled);
                var updated = reconciler.Reconcile(old, next, native, () => { })!;
                Check(ReferenceEquals(native, updated), "Launch action component must stay mounted as configuration arrives.");
                old = next; native = updated;
            }
            Element Slots(bool selected) => VStack(8,
                TextBlock("Stable sibling").WithKey("sibling"),
                BrowserUi.Slot("selection", selected ? Button("Unselect", () => { }) : TextBlock("")),
                BrowserUi.Slot("files", selected ? Expander("App files", TextBlock("Files")) : TextBlock("")));
            old = Slots(false);
            native = reconciler.Mount(old, () => { })!;
            var sibling = ((StackPanel)native).Children[0];
            foreach (var selected in new[] { true, false, true, false })
            {
                var next = Slots(selected);
                native = reconciler.Reconcile(old, next, native, () => { })!;
                Check(ReferenceEquals(((StackPanel)native).Children[0], sibling), "Selection changes must retain unrelated sibling controls.");
                old = next;
            }
            // Exercise actual detail layouts across library, app, and section transitions.
            Element Detail(InstalledApp? selected, string view) => Component<ApplicationDetailPane, ApplicationDetailPaneProps>(
                new(selected, [], null, new([], []), null, null, "", view, false, "", _ => { }, () => { }, _ => { }, _ => { }, _ => { }))
                .WithKey("detail");
            old = Detail(null, "Overview"); native = reconciler.Mount(old, () => { })!;
            foreach (var (selected, view) in new (InstalledApp?, string)[] { (app, "Overview"), (app, "Settings"),
                (app, "LocalState"), (app, "Registry"), (app, "Package"), (app, "Storage"), (null, "Overview") })
            {
                var next = Detail(selected, view);
                var updated = reconciler.Reconcile(old, next, native, () => { })!;
                Check(ReferenceEquals(native, updated), "Detail pane must retain its mounted root across sections.");
                var tabs = Descendants(updated).OfType<Button>().Where(button =>
                    new[] { "View Overview", "View Package", "View Storage", "View Settings", "View Registry", "View LocalState" }.Contains(Microsoft.UI.Xaml.Automation.AutomationProperties.GetName(button))).ToArray();
                Check(tabs.Length == 6 && tabs.Count(button => Microsoft.UI.Xaml.Automation.AutomationProperties.GetItemStatus(button) == "Selected") == 1,
                    "Exactly one section must be selected after every navigation transition: " + string.Join(", ", tabs.Select(button => $"{Microsoft.UI.Xaml.Automation.AutomationProperties.GetName(button)}={Microsoft.UI.Xaml.Automation.AutomationProperties.GetItemStatus(button)}")));
                Check(Microsoft.UI.Xaml.Automation.AutomationProperties.GetName(tabs.Single(button =>
                    Microsoft.UI.Xaml.Automation.AutomationProperties.GetItemStatus(button) == "Selected")) == $"View {view}",
                    "The selected section must match the current content.");
                old = next; native = updated;
            }
            // Edit actual native text fields and run the host's next reconciliation pass.
            // Neither an unrelated native row nor its unsaved draft may be replaced.
            var token = new object();
            var first = new AppSetting("LocalSettings", "First", "String", "original", new("LocalSettings", [], "First"));
            var second = new AppSetting("LocalSettings", "Second", "String", "sibling", new("LocalSettings", [], "Second"));
            Element Editors() => VStack(8,
                Component<SettingEditor, SettingEditorProps>(new("fixture", first, token, false)).WithKey("first"),
                Component<SettingEditor, SettingEditorProps>(new("fixture", second, token, false)).WithKey("second"));
            old = Editors(); native = reconciler.Mount(old, () => { })!;
            var firstInput = Descendants(native).OfType<TextBox>().Single(control =>
                Microsoft.UI.Xaml.Automation.AutomationProperties.GetName(control) == "First");
            var siblingInput = Descendants(native).OfType<TextBox>().Single(control =>
                Microsoft.UI.Xaml.Automation.AutomationProperties.GetName(control) == "Second");
            var siblingRow = ((StackPanel)native).Children[1];
            siblingInput.Text = "Unsaved sibling draft";
            var edited = Editors(); native = reconciler.Reconcile(old, edited, native, () => { })!; old = edited;
            firstInput.Text = "Changed first value";
            edited = Editors(); native = reconciler.Reconcile(old, edited, native, () => { })!;
            Check(ReferenceEquals(siblingRow, ((StackPanel)native).Children[1]), "Editing one setting must retain the sibling's mounted row.");
            Check(ReferenceEquals(siblingInput, Descendants(native).OfType<TextBox>().Single(control =>
                Microsoft.UI.Xaml.Automation.AutomationProperties.GetName(control) == "Second")), "Editing one setting must retain the sibling's native input.");
            Check(siblingInput.Text == "Unsaved sibling draft", "Editing one setting must preserve a sibling's unsaved draft.");
            Check(firstInput.Text == "Changed first value", "Edited value must survive reconciliation.");
            var numeric = first with { Type = "Int32", Value = "1048" };
            var boolean = second with { Type = "Boolean", Value = "False" };
            Element TypedEditors() => VStack(8,
                Component<SettingEditor, SettingEditorProps>(new("fixture", numeric, token, false)).WithKey("numeric"),
                Component<SettingEditor, SettingEditorProps>(new("fixture", boolean, token, false)).WithKey("boolean"));
            old = TypedEditors(); native = reconciler.Mount(old, () => { })!;
            var numericInput = Descendants(native).OfType<NumberBox>().Single();
            var booleanInput = Descendants(native).OfType<ToggleSwitch>().Single();
            booleanInput.IsOn = true;
            edited = TypedEditors(); native = reconciler.Reconcile(old, edited, native, () => { })!; old = edited;
            numericInput.Text = "1200.5";
            edited = TypedEditors(); native = reconciler.Reconcile(old, edited, native, () => { })!; old = edited;
            var numericRow = ((StackPanel)native).Children[0];
            Check(!Descendants(numericRow).OfType<Button>().Single(control =>
                Microsoft.UI.Xaml.Automation.AutomationProperties.GetName(control) == "Save").IsEnabled,
                "Fractional integer drafts must disable Save without changing the stored type.");
            numericInput.Text = "1200";
            edited = TypedEditors(); native = reconciler.Reconcile(old, edited, native, () => { })!;
            Check(Descendants(((StackPanel)native).Children[0]).OfType<Button>().Single(control =>
                Microsoft.UI.Xaml.Automation.AutomationProperties.GetName(control) == "Save").IsEnabled,
                "Valid numeric edits must enable Save.");
            Check(ReferenceEquals(numericInput, Descendants(native).OfType<NumberBox>().Single()), "Numeric updates must retain the NumberBox control.");
            Check(ReferenceEquals(booleanInput, Descendants(native).OfType<ToggleSwitch>().Single()) && booleanInput.IsOn,
                "Numeric updates must preserve a sibling Boolean toggle and its unsaved value.");
            var failed = ErrorBoundary(Component<ThrowingView>(), error =>
                Component<ErrorRecoveryPanel, ErrorRecoveryPanelProps>(new("View unavailable", error, () => { }, () => { })));
            native = reconciler.Mount(failed, () => { })!;
            var actions = Descendants(native).OfType<Button>().Select(control =>
                Microsoft.UI.Xaml.Automation.AutomationProperties.GetName(control)).ToArray();
            Check(actions.Contains("Restart WinApps") && actions.Contains("Try again") && actions.Contains("Open Apps List"),
                "A render failure must provide restart, retry, and navigation actions.");
            Check(Descendants(native).OfType<Expander>().Any(control => !control.IsExpanded), "Technical details must start collapsed.");
            Element Boundary(bool broken, int attempt) => VStack(6,
                TextBlock("Unaffected area").WithKey("unaffected"),
                ErrorBoundary(Button("Healthy view", () => { }).WithKey($"view-{attempt}").Set(_ =>
                {
                    if (broken) throw new InvalidCastException("Synthetic native update failure.");
                }), error => Component<ErrorRecoveryPanel, ErrorRecoveryPanelProps>(new("View unavailable", error, () => { }, null)))
                    .WithKey("boundary"));
            old = Boundary(false, 0); native = reconciler.Mount(old, () => { })!;
            var unaffected = ((StackPanel)native).Children[0];
            var brokenView = Boundary(true, 0);
            native = reconciler.Reconcile(old, brokenView, native, () => { })!;
            Check(Descendants(native).OfType<Button>().Any(control =>
                Microsoft.UI.Xaml.Automation.AutomationProperties.GetName(control) == "Restart WinApps"),
                "A native update exception must be contained by the friendly recovery panel.");
            Check(ReferenceEquals(unaffected, ((StackPanel)native).Children[0]), "An area failure must preserve its unaffected sibling.");
            var recoveredView = Boundary(false, 1);
            native = reconciler.Reconcile(brokenView, recoveredView, native, () => { })!;
            Check(!Descendants(native).OfType<Button>().Any(control =>
                Microsoft.UI.Xaml.Automation.AutomationProperties.GetName(control) == "Restart WinApps"),
                "Explicit retry must replace the failed area with a healthy view.");
            Console.WriteLine($"Passed {checks} native UI reconciliation checks.");
            File.AppendAllText("artifacts/ui-checks.log", $"Passed {checks} native UI reconciliation checks.\n");
            Environment.ExitCode = 0;
        }
        catch (Exception error)
        {
            Console.Error.WriteLine(error);
            File.AppendAllText("artifacts/ui-checks.log", error.ToString());
            Environment.ExitCode = 1;
        }
        finally { _window.Close(); Exit(); }
    }

    private static IEnumerable<UIElement> Descendants(UIElement root)
    {
        yield return root;
        IEnumerable<UIElement> children = root switch
        {
            Panel panel => panel.Children,
            Border border when border.Child is not null => [border.Child],
            ContentControl control when control.Content is UIElement child => [child],
            ScrollViewer scroll when scroll.Content is UIElement child => [child],
            ScrollView scroll when scroll.Content is UIElement child => [child],
            _ => []
        };
        foreach (var child in children)
            foreach (var descendant in Descendants(child)) yield return descendant;
    }

    // Keep timer assertions on WinUI's dispatcher even when the bare reconciler
    // installs or restores a synchronization context during a native update.
    private readonly struct UiDelay(int milliseconds) : System.Runtime.CompilerServices.INotifyCompletion
    {
        public UiDelay GetAwaiter() => this;
        public bool IsCompleted => false;
        public void GetResult() { }
        public void OnCompleted(Action continuation)
        {
            var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(milliseconds) };
            timer.Tick += Tick;
            void Tick(object? sender, object args)
            {
                timer.Stop();
                timer.Tick -= Tick;
                continuation();
            }
            timer.Start();
        }
    }
}

internal sealed class ThrowingView : Component
{
    public override Element Render() => throw new InvalidOperationException("Synthetic render failure.");
}

internal sealed class TestDispatcherContext(Microsoft.UI.Dispatching.DispatcherQueue dispatcher) : SynchronizationContext
{
    public override void Post(SendOrPostCallback callback, object? state) => dispatcher.TryEnqueue(() => callback(state));
    public override SynchronizationContext CreateCopy() => new TestDispatcherContext(dispatcher);
}
