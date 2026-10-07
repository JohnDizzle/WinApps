namespace MyApps.Models;

public sealed record FolderFootprint(string Label, string Path, long Bytes, long Files, int Skipped, string State)
{
    public string Summary => $"{Label}: {Bytes / 1048576d:N2} MiB in {Files:N0} files · {State} · {Skipped} entries skipped";
}
