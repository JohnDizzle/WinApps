// SPDX-License-Identifier: GPL-3.0-only
using Microsoft.UI.Reactor;
using WinApps.Models;
using System.Security.Cryptography;
using System.Text;

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
        if (argument is null)
        {
            try
            {
                var activation = Microsoft.Windows.AppLifecycle.AppInstance.GetCurrent().GetActivatedEventArgs();
                if (activation.Data is Windows.ApplicationModel.Activation.ILaunchActivatedEventArgs launch)
                    argument = launch.Arguments.Trim();
            }
            catch (Exception error) { WindowDiagnostics.Write("Jump List activation", error); }
        }
        return ParseRequest(argument);
    }

    internal static string? ParseRequest(string? argument)
    {
        if (argument is null || !argument.StartsWith(Prefix, StringComparison.Ordinal)) return null;
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
                var signature = string.Join("|", apps.Select(app => $"{app.FullName}:{app.Name}:{app.IconUri}"));
                if (signature == _signature) continue;
                try
                {
                    if (Windows.UI.StartScreen.JumpList.IsSupported())
                    {
                        // Like Linkscape, use app-local logo URIs for dynamic packaged icons.
                        var list = await Windows.UI.StartScreen.JumpList.LoadCurrentAsync();
                        list.SystemGroupKind = Windows.UI.StartScreen.JumpListSystemGroupKind.None;
                        list.Items.Clear();
                        foreach (var app in apps)
                        {
                            var item = Windows.UI.StartScreen.JumpListItem.CreateWithArguments(
                                OverviewArguments(app), app.Name);
                            item.GroupName = "Suggested apps";
                            item.Description = $"Open {app.Name} overview in WinApps";
                            item.Logo = await CacheLogoAsync(app);
                            list.Items.Add(item);
                        }
                        await list.SaveAsync();
                    }
                    else
                    {
                        JumpList.AppUserModelId = "JohnDizzle.WinApps";
                        await JumpList.UpdateAsync(apps.Select(app => new JumpListItem(app.Name,
                            OverviewArguments(app), JumpListItemKind.Custom,
                            Description: $"Open {app.Name} overview in WinApps",
                            Icon: WindowIcon.FromPath(Path.Combine(AppContext.BaseDirectory, "Assets", "WinApps.ico")),
                            GroupCategory: "Suggested apps")));
                    }
                    _signature = signature;
                }
                catch (Exception error) { WindowDiagnostics.Write("Suggestion Jump List", error); }
            }
        }
        finally { _updating = false; }
    }

    private static string OverviewArguments(InstalledApp app) =>
        Prefix + Convert.ToBase64String(Encoding.UTF8.GetBytes(app.FullName));

    private static async Task<Uri> CacheLogoAsync(InstalledApp app)
    {
        try
        {
            if (Uri.TryCreate(app.IconUri, UriKind.Absolute, out var source) && source.IsFile && File.Exists(source.LocalPath))
            {
                var sourcePath = source.LocalPath;
                var key = $"{app.FullName}|{sourcePath}|{File.GetLastWriteTimeUtc(sourcePath).Ticks}";
                var name = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key))) + ".png";
                var folder = Path.Combine(Windows.Storage.ApplicationData.Current.LocalFolder.Path, "JumpListIcons");
                Directory.CreateDirectory(folder);
                var destination = Path.Combine(folder, name);
                if (!File.Exists(destination))
                {
                    var bytes = await File.ReadAllBytesAsync(sourcePath);
                    await File.WriteAllBytesAsync(destination, bytes);
                }
                return new Uri($"ms-appdata:///local/JumpListIcons/{name}");
            }
        }
        catch (Exception error) { WindowDiagnostics.Write("Jump List logo", error); }
        return new Uri("ms-appx:///Assets/Square44x44Logo.png");
    }
}
