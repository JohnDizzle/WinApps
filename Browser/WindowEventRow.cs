// SPDX-License-Identifier: GPL-3.0-only
using WinApps.Services.Inventory;

namespace WinApps.Browser;

[Microsoft.UI.Xaml.Data.Bindable]
internal sealed class WindowEventRow(WindowEventEntry entry)
{
    internal WindowEventEntry Entry => entry;
    public string Time => entry.Time.ToString("HH:mm:ss.fff");
    public string Event => entry.Name;
    public string Title => entry.Title;
    public string Handle => $"0x{entry.Window:X}";
    public string Details => $"Thread {entry.Thread} · object {entry.ObjectId} · child {entry.ChildId}";
    public override string ToString() => entry.ToString();
}
