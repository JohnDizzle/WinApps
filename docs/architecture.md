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
