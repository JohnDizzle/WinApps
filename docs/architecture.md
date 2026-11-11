# Architecture

See the [application selection and property navigation diagram](navigation-flow.md) for the user flow and component state rules.

WinApps is the public project name. The current executable, project files, C# namespace, and local cache directory retain the `MyApps` name.

```text
App.cs                       Reactor application entry point
Browser/                     UI components and shared visual helpers
Models/                      Immutable application, file, settings, and package records
Services/
  Inventory/                 Desktop discovery and library cache
  Packages/                  MSIX discovery, manifest inspection, and application launch
  Settings/                  ApplicationData readers/writers and typed validation
  Storage/                   File inspection and logical footprint measurement
  Icons/                     Application icon extraction
tests/
  DataInspection/            Portable checks using synthetic files/settings
  PackageIntegration/        Read-only checks against locally installed Windows packages
  UiIntegration/             Native control reconciliation and edit-isolation checks
docs/                        Contributor-facing architecture and release notes
.github/                     CI and contribution templates
```

`PackageExplorer` coordinates discovery, selection, inspection, and theme state. Stable keys keep `ApplicationRail` and `ApplicationDetailPane` mounted independently. The rail owns search, alphabet, and expander state. The detail pane displays the active section and embeds independent launch and settings components.

`PackageSettingsPanel` owns its snapshot and reload action. Each `SettingEditor` owns its expected value, draft, validation, and save status. Editing a field does not write root state. Save updates an existing exact ApplicationData location through `PackageSettingsWriter`; arrays and composite members remain read only. NumberBox controls handle ordinary numeric types, while exact 64-bit editors avoid Double precision loss.

Library metadata is cached locally. Files, decoded settings, and manifest configuration are inspected live on selection. Resource packages are correlated by current-user package family and inspected separately. Manifest declarations do not prove binary UI framework usage or runtime token permissions.

Keep Windows calls in services and pure parsers/codecs independently testable. Launch actions must resolve the selected registered application, and elevation must be checked again against the installed manifest before invoking UAC.

`WinAppsShell` and the rail/detail boundaries contain display errors. Recovery panels offer retry, restart, and navigation with collapsed technical details. Conditional controls live in stable containers with native-type-specific child keys. Ordinary value changes preserve component identities; explicit retry rebuilds only the failed area.

## Reactor lifecycle and UI properties

Use declarative event modifiers such as `PaneToggleRequested` and `OnSizeChanged` when Reactor provides them. Let the renderer replace and detach those handlers. For native events without a declarative bridge, subscribe in a component-owned `UseEffect` and return cleanup that removes the named handlers. The explorer's `Loaded`, `Unloaded`, and `XamlRoot.Changed` bridge follows this pattern and detaches from the old root on unload and component unmount.

Use `ToolTip` / `WithToolTip`, `ItemStatus`, `BorderThickness`, and `IsVisible` for reconciled properties. Keep `.Set(...)` for native configuration without an equivalent DSL modifier or for capturing control references, rather than attaching lifetime-owned subscriptions. Flex layout uses `FlexPadding`; rounded backgrounds and borders belong on a native `Border` wrapping the flex content.

The four Reactor diagnostics covering these mistakes are errors for Browser sources in `.editorconfig`. They must be fixed rather than suppressed. These conventions follow [Reactor's effect lifecycle](https://github.com/microsoft/microsoft-ui-reactor/blob/main/docs/guide/hooks.md#useeffect) and [Microsoft's event unsubscription guidance](https://learn.microsoft.com/en-us/dotnet/csharp/programming-guide/events/how-to-subscribe-to-and-unsubscribe-from-events).
