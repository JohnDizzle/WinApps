# WinApps Store identity

These values were supplied by the maintainer's Partner Center product identity screenshot. They are identifiers, not signing credentials. The current project runs unpackaged (`WindowsPackageType=None`); these values are recorded for future MSIX packaging and Store association.

| Field | Value |
| --- | --- |
| Product display name | WinApps |
| Package/Identity/Name | `48996JohnM.Doyle.WinApps` |
| Package/Identity/Publisher | `CN=B61100C9-E803-4D04-BE74-91A6FB862E5C` |
| Package/Properties/PublisherDisplayName | `John M. Doyle` |
| Package family name | `48996JohnM.Doyle.WinApps_jw9ac53qrz6qt` |
| Store product ID | `9NWZ6MZV4BVN` |
| Store URL | <https://apps.microsoft.com/detail/9NWZ6MZV4BVN> |
| Store protocol link | `ms-windows-store://pdp/?productid=9NWZ6MZV4BVN` |

The recorded Store link does not establish that a release is published or available. Confirm the identity in Partner Center when preparing a submission. Package version, architecture, application Id, capabilities, executable path, and signing/distribution configuration still need to be specified by that packaging work.

Package family name and SID are derived identities; they are not additional `Identity` manifest attributes. No MSA client ID is configured because the app currently has no Microsoft-account authentication feature.
