// SPDX-License-Identifier: GPL-3.0-only
namespace MyApps.Services.Inventory;

public static class ApplicationFilter
{
    public static bool IsSupportingComponent(string name)
    {
        return System.Text.RegularExpressions.Regex.IsMatch(name,
            @"(?ix) ^Microsoft\s+Visual\s+C\+\+.*(?:Redistributable|Runtime|Minimum|Additional) | ^Microsoft\s+(?:\.NET|ASP\.NET|Windows\s+Desktop)\s+.*(?:Runtime|Host|Targeting|AppHost) | ^Microsoft\s+Edge\s+WebView2\s+Runtime | ^(?:Windows|Microsoft\s+Windows)\s+.*(?:SDK|Software\s+Development\s+Kit) | ^Microsoft\s+.*(?:VC\s+Runtime|VCLibs)");
    }
}
