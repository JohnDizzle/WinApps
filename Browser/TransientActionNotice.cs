// SPDX-License-Identifier: GPL-3.0-only
using Microsoft.UI.Reactor;
using Microsoft.UI.Reactor.Core;
using Microsoft.UI.Xaml;
using static Microsoft.UI.Reactor.Factories;
using static WinApps.Browser.BrowserUi;

namespace WinApps.Browser;

internal sealed record TransientActionNoticeProps(string Message, int Version = 0);

internal sealed class TransientActionNotice : Component<TransientActionNoticeProps>
{
    public override Element Render()
    {
        UseEffect(() =>
        {
            ActionNotificationStack.Queue.Add(Props.Message, DateTimeOffset.UtcNow);
        }, Props.Message, Props.Version);
        return VStack(0).IsVisible(false);
    }
}
