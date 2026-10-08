// SPDX-License-Identifier: GPL-3.0-only
using Microsoft.UI.Reactor;
using Microsoft.UI.Reactor.Core;
using Microsoft.UI.Xaml;
using static Microsoft.UI.Reactor.Factories;
using static MyApps.Browser.BrowserUi;

namespace MyApps.Browser;

internal sealed record TransientActionNoticeProps(string Message, int Version = 0);

internal sealed class TransientActionNotice : Component<TransientActionNoticeProps>
{
    public override Element Render()
    {
        var visible = UseState(false);
        UseEffect(() =>
        {
            visible.Set(Props.Message.Length > 0);
            var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
            timer.Tick += Tick;
            void Tick(object? sender, object args) { timer.Stop(); visible.Set(false); }
            if (Props.Message.Length > 0) timer.Start();
            return () => { timer.Stop(); timer.Tick -= Tick; };
        }, Props.Message, Props.Version);
        return Text(Props.Message).IsVisible(visible.Value);
    }
}
