// SPDX-License-Identifier: GPL-3.0-only
using Microsoft.UI.Reactor;
using WinApps.Models;

namespace WinApps.Browser;

internal static class SuggestionJumpList
{
    private const string Prefix = "--overview=";
    private static string? _signature;
    private static InstalledApp[]? _pending;
    private static bool _updating;
    internal static string? RequestedApp { get; private set; } = ReadRequest();

    private static string? ReadRequest()
    {
        var argument = Environment.GetCommandLineArgs().FirstOrDefault(value => value.StartsWith(Prefix, StringComparison.Ordinal));
        if (argument is null) return null;
        try { return System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(argument[Prefix.Length..])); }
        catch (FormatException) { return null; }
    }

    internal static string? TakeRequest()
    {
        var request = RequestedApp;
        RequestedApp = null;
        return request;
    }

    internal static async void Update(InstalledApp[] suggestions)
    {
        _pending = suggestions.Take(6).ToArray();
        if (_updating) return;
        _updating = true;
        try
        {
            while (_pending is { } apps)
            {
                _pending = null;
                var signature = string.Join("|", apps.Select(app => $"{app.FullName}:{app.Name}"));
                if (signature == _signature) continue;
                try
                {
                    JumpList.AppUserModelId = "JohnDizzle.WinApps";
                    await JumpList.UpdateAsync(apps.Select(app => new JumpListItem(app.Name,
                        Prefix + Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(app.FullName)),
                        JumpListItemKind.Custom, Description: $"Open {app.Name} overview in WinApps",
                        GroupCategory: "Suggested apps")));
                    _signature = signature;
                }
                catch (Exception error) { WindowDiagnostics.Write("Suggestion Jump List", error); }
            }
        }
        finally { _updating = false; }
    }
}
