using Microsoft.UI.Reactor;
using MyApps.Browser;

namespace MyApps.Application;

internal static class Program
{
    [STAThread]
    private static void Main() => ReactorApp.Run<PackageExplorer>("MyApps", width: 1100, height: 780);
}
