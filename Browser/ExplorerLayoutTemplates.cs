// SPDX-License-Identifier: GPL-3.0-only
using Microsoft.UI.Reactor;
using Microsoft.UI.Reactor.Core;
using static Microsoft.UI.Reactor.Factories;

namespace WinApps.Browser;

// Both templates retain the same keyed child components. Resizing changes
// arrangement rather than recreating the application selection or file tree.
internal static class ExplorerLayoutTemplates
{
    public static FlexElement Render(bool compact, Element navigation, Element details) =>
        (compact ? Compact(navigation, details) : Wide(navigation, details))
            .WithKey("explorer-layout")
            .Set(control => BrowserMotion.LayoutTransition(control, compact));

    private static FlexElement Wide(Element navigation, Element details) => FlexRow(navigation, details);
    private static FlexElement Compact(Element navigation, Element details) => FlexColumn(navigation, details);
}
