// SPDX-License-Identifier: GPL-3.0-only
using WinApps.Models;

namespace WinApps.Services.Inventory;

internal static class RunningApplicationMatch
{
    internal static InstalledApp? Find(string executable, string name, IReadOnlyList<InstalledApp> apps)
    {
        var candidates = apps.Where(app => !string.IsNullOrWhiteSpace(app.InstallPath) && executable.Length > 0 &&
            executable.StartsWith(app.InstallPath.TrimEnd('\\', '/') + "\\", StringComparison.OrdinalIgnoreCase)).ToArray();
        if (candidates.Length > 0)
        {
            var longest = candidates.Max(app => app.InstallPath.TrimEnd('\\', '/').Length);
            candidates = candidates.Where(app => app.InstallPath.TrimEnd('\\', '/').Length == longest).ToArray();
        }
        else candidates = apps.Where(app => string.Equals(app.Name, name, StringComparison.OrdinalIgnoreCase)).ToArray();
        if (candidates.Length == 1) return candidates[0];
        var exact = candidates.Where(app => string.Equals(app.Name, name, StringComparison.OrdinalIgnoreCase)).ToArray();
        return exact.Length == 1 ? exact[0] : null;
    }
}
