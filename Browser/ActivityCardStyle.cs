// SPDX-License-Identifier: GPL-3.0-only
using Microsoft.UI.Reactor;
using Microsoft.UI.Reactor.Core;
using static Microsoft.UI.Reactor.Factories;

namespace WinApps.Browser;

internal static class ActivityCardStyle
{
    internal static BorderElement Section(string title, params Element[] children) => Border(VStack(8,
        new[] { TextBlock(title).FontSize(20).Bold() }.Concat(children).ToArray()).Padding(16))
        .Background(Theme.CardBackground).WithBorder(Theme.CardStroke).CornerRadius(12);

    // Opaque theme surfaces keep cards readable above the window's tinted material.
    internal static BorderElement Surface(Element content) => Border(content)
        .Background(Theme.Ref("SolidBackgroundFillColorSecondaryBrush"))
        .WithBorder(Theme.Ref("ControlStrokeColorDefaultBrush"))
        .CornerRadius(16);

    internal static Element AccentLine() => Border(null).Height(3)
        .Background(Theme.Ref("AccentFillColorDefaultBrush")).CornerRadius(2);

    internal static Element Badge(string label) => Border(TextBlock(label).FontSize(12).SemiBold()
        .Foreground(Theme.Ref("TextOnAccentFillColorPrimaryBrush"))).Padding(8, 4)
        .Background(Theme.Ref("AccentFillColorDefaultBrush")).CornerRadius(6);
}
