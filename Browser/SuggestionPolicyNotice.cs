// SPDX-License-Identifier: GPL-3.0-only
using System.Diagnostics;
using Microsoft.UI.Reactor;
using Microsoft.UI.Reactor.Core;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using static Microsoft.UI.Reactor.Factories;

namespace WinApps.Browser;

internal sealed class SuggestionPolicyNotice : Component
{
    private const string Revision = "2026-10-18-app-suggestions-v1";
    private static string NoticePath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "WinApps", "suggestion-policy-notice.txt");
    private readonly bool _acknowledged = IsAcknowledged();
    private static bool IsAcknowledged()
    {
        try { return new FileInfo(NoticePath) is { Exists: true, Length: < 256 } && File.ReadAllText(NoticePath) == Revision; }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException) { return false; }
    }
    internal static void OpenPolicy()
    {
        try
        {
            var uri = new Uri(Path.Combine(AppContext.BaseDirectory, "privacy.html")).AbsoluteUri + "#app-interaction-history";
            Process.Start(new ProcessStartInfo(uri) { UseShellExecute = true });
        }
        catch (Exception error)
        {
            StackBehaviorNotification.Queue.Add($"Could not open the suggestion policy: {error.Message}", DateTimeOffset.UtcNow);
        }
    }
    public override Element Render()
    {
        var dismissed = UseState(_acknowledged);
        return InfoBar("Policy update", "Optional app-interaction logging is now available in Settings. It is off by default and stays on this PC.")
            .Informational().IsClosable()
            .Set(control =>
            {
                control.IsOpen = !dismissed.Value;
                if (control.ActionButton is null)
                {
                    var link = new HyperlinkButton { Content = "Read policy change" };
                    link.Click += (_, _) => OpenPolicy();
                    control.ActionButton = link;
                }
            })
            .Closed(() =>
            {
                dismissed.Set(true);
                try
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(NoticePath)!);
                    File.WriteAllText(NoticePath, Revision);
                }
                catch (Exception error) when (error is IOException or UnauthorizedAccessException) { }
            })
            .IsVisible(!dismissed.Value).Margin(left: 16, top: 8, right: 16).Flex(shrink: 0);
    }
}
