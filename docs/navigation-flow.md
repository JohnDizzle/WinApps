# Application selection and property navigation

Select a product (installed application) in the left rail. Its identity becomes the shared context for the detail views and the file tree beneath its rail entry.

```mermaid
flowchart TD
    Library[Application library on this PC] --> Find[Search or browse alphabetically]
    Find --> Select[Select product / application]
    Select --> Header[App icon, name, type, and actions]
    Select --> Files[App files in the left rail]
    Header --> Properties[Choose a property view]
    Header --> Manage[Windows Advanced options / Installed apps]
    Header --> Launch[Launch packaged application]

    Properties --> Overview[Overview]
    Overview --> Info[Version, publisher, source, and identity]
    Overview --> Locations[Installed folder and app data folder]

    Properties --> Package[Package]
    Package --> Manifest[Manifest, runtime, trust, and capabilities]
    Package --> Resources[Related resource packages]
    Package --> Frameworks[Packaging and UI framework documentation]
    Package --> Layout{Property layout}
    Layout --> Grid[Grid: detailed cards]
    Layout --> Rows[Rows: expandable groups]
    Layout --> Widgets[Widgets: summaries and knowledge links]

    Properties --> Storage[Storage]
    Storage --> Measure[Measure accessible files and folder sizes]

    Properties --> Settings[Settings]
    Settings --> Values[Read ApplicationData keys and typed values]
    Values --> Draft[Edit a supported value]
    Draft --> Validate[Validate type and expected stored value]
    Validate --> Save[Explicit Save]

    Properties --> Registry[Registry]
    Registry --> Hives[Discover hive files; contents are not decoded]

    Properties --> LocalState[LocalState]
    LocalState --> Data[Browse persistent app files]

    Files --> Package
    Files --> Settings
    Files --> Registry
    Files --> LocalState
    Data --> Preview[Preview selected file / open with default app]
```

## Navigation and state rules

- The section tabs and Back to top remain above the detail scrolling area. The Grid / Rows / Widgets selector appears above Packaging and UI frameworks in Package view.
- Selecting another application loads its inspection data. Switching a property layout only changes presentation; it must not rescan files or refresh the rail.
- Lists and collections use data-driven layout templates with stable identity keys. The app rail keys each item by its application identity; files use their full paths.
- Conditional true/false layouts belong in the layout template. Use `.Set()` for native properties and behavior, rather than constructing or replacing child layouts.
- Interactive controls have descriptive AutomationName labels. Unrelated updates preserve focus, draft values, expansion state, and mounted controls.
- Motion stays scoped to the component being changed and follows Windows animation preferences.
- Settings writes occur only through explicit Save. Package metadata, manifest declarations, and file previews are read only. Opening Windows Settings or an external app delegates subsequent actions to that application.

Desktop entries without package identity may have fewer available details. MSIX packaging and UI frameworks are separate concepts; manifest data does not always identify the UI framework.

Documentation links, authored property explanations, and layout labels/icons are configured in [UI resources](ui-resources.md). The future optional assistance idea is recorded in [future settings assistance](future-settings-assistance.md).
