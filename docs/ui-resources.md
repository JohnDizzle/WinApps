# Customize links and layout controls

Edit `Resources/ui-resources.json` beside the running WinApps executable, then click **Reload resources** in its toolbar. Hover over that button to see the exact file path. No rebuild is required for edits to this runtime file. Edits to the repository copy are copied into build/publish output on the next build.

- `Sections`: property group explanations, documentation topics, and links. Keys match the group titles; `Default` covers unknown groups.
- `Links`: the MSIX, WinUI 3, frameworks, and runtime documentation buttons.
- `Controls`: Grid, Rows, and Widgets labels and icons. Keep their stable dictionary keys when renaming the visible labels.

Links must be absolute HTTPS URLs. Keep JSON syntax valid. Reload failures show a message and retain the previous resources. Startup uses bundled defaults if the external file cannot be loaded. Links do not update from the internet automatically; edit this file when a documentation address changes.

The file customizes documentation and the three layout controls, not the package's actual identity, values, activation behavior, or all application text.
