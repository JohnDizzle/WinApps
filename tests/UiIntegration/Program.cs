// SPDX-License-Identifier: GPL-3.0-only
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Reactor.Core;
using Microsoft.UI.Reactor;
using MyApps.Browser;
using MyApps.Models;
using static Microsoft.UI.Reactor.Factories;

namespace MyApps.UiIntegration;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
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
            var reconciler = new Reconciler();
            var checks = 0;
            void Check(bool value, string message)
            {
                if (!value) throw new InvalidOperationException(message);
                checks++;
            }
            var placementPath = Path.GetFullPath(Path.Combine("artifacts", "window-placement-check-" + Guid.NewGuid().ToString("N") + ".json"));
            var placementStore = new MyApps.Services.Storage.WindowPlacementStore(placementPath);
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
            var railApps = new[] { app };
            var loadingDetails = Component<ApplicationDetailPane, ApplicationDetailPaneProps>(new(app, [], null, null, null,
                null, "", "Overview", true, "Loading", _ => { }, () => { }, _ => { }, _ => { }, _ => { }, true));
            var loadingNative = reconciler.Mount(loadingDetails, () => { })!;
            Check(Descendants(loadingNative).OfType<ProgressRing>().Single(ring => ring.IsActive).IsActive,
                "App inspection must show an active loading indicator in the detail pane.");
            Check(Descendants(loadingNative).OfType<Button>()
                .Where(button => new[] { "View Overview", "View Package", "View Storage", "View Settings", "View Registry", "View LocalState" }.Contains(Microsoft.UI.Xaml.Automation.AutomationProperties.GetName(button)))
                .All(button => button.IsEnabled),
                "Section tabs must remain interactive while app inspection runs.");
            var railInventory = new AppDataInventory([], []);
            Element Rail() => Component<ApplicationRail, ApplicationRailProps>(new(railApps, app, railInventory,
                "Package", false, _ => { }, () => { }, () => { }, _ => { }, _ => { }, _ => { })).WithKey("rail");
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
                using var normalizedIcon = MyApps.Services.Icons.ApplicationIcons.NormalizePackageIcon(paddedIcon);
                Check(normalizedIcon.Width == 64 && normalizedIcon.Height == 64,
                    "Normalized package icons must retain a consistent canvas.");
                Check(normalizedIcon.GetPixel(5, 32).A > 200 && normalizedIcon.GetPixel(32, 5).A == 0,
                    "Padded rectangular artwork must enlarge without losing its aspect ratio.");
                Check(normalizedIcon.GetPixel(0, 32).A == 0,
                    "Normalized artwork must retain an outer margin.");
            }
            using (var emptyIcon = new System.Drawing.Bitmap(32, 32))
            using (var normalizedEmpty = MyApps.Services.Icons.ApplicationIcons.NormalizePackageIcon(emptyIcon))
                Check(normalizedEmpty.GetPixel(32, 32).A == 0,
                    "Empty icons must remain transparent without invalid cropping.");
            Console.WriteLine("Passed package icon normalization checks.");
            if (Environment.GetCommandLineArgs().Contains("--action-notices"))
            {
                var noticeText = "First action completed";
                Element Notice() => VStack(0,
                    Component<ActionNotificationStack>().WithKey("stack"),
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
                var noticeControl = Descendants(noticeNative).OfType<TextBlock>().Single(text => text.Text == noticeText);
                Check(noticeControl.Visibility == Visibility.Visible, "Action notices must initially be visible.");
                noticeText = "Second action completed";
                RenderNotice();
                await new UiDelay(100);
                Check(Descendants(noticeNative).OfType<TextBlock>().Count(text => text.Text.EndsWith("action completed")) == 2,
                    "Notifications must stack instead of replacing the previous app's message.");
                await new UiDelay(3000);
                Check(Descendants(noticeNative).OfType<TextBlock>().Any(text => text.Text == noticeText) &&
                    !Descendants(noticeNative).OfType<TextBlock>().Any(text => text.Text == "First action completed"),
                    "An earlier timer must not dismiss a newer action notice.");
                await new UiDelay(2500);
                Check(!Descendants(noticeNative).OfType<TextBlock>().Any(text => text.Text.EndsWith("action completed")),
                    "Action notices must clear after five seconds.");
                Check(ReferenceEquals(noticeRoot, noticeNative), "Notice expiry must retain its native root.");
                var uninstallCallbacks = 0;
                var actionTree = Component<ApplicationLaunchActions, ApplicationLaunchActionsProps>(new(app, null, _ => uninstallCallbacks++));
                var actionNative = reconciler.Mount(actionTree, () => { })!;
                Check(uninstallCallbacks == 0 && !Descendants(actionNative).OfType<Button>().Any(button => button.Content as string == "Confirm uninstall"),
                    "Mounting actions must neither uninstall an app nor show inline confirmation controls.");
                var desktopResult = new MyApps.Services.Packages.ApplicationUninstaller().UninstallAsync(app with { FamilyName = "" }).GetAwaiter().GetResult();
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
            Check(new MyApps.Services.Packages.ApplicationUninstaller().UninstallAsync(app with { FamilyName = "" })
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
