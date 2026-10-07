namespace MyApps.Services.Packages;

public static class PackageLaunchPath
{
    public static string Resolve(string installPath, string executable)
    {
        if (string.IsNullOrWhiteSpace(executable) || Path.IsPathRooted(executable) || executable.Contains(':'))
            throw new IOException("The manifest executable must be a relative package path.");
        var root = Path.GetFullPath(installPath).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var path = Path.GetFullPath(Path.Combine(root, executable));
        if (!path.StartsWith(root, StringComparison.OrdinalIgnoreCase) || !Path.GetExtension(path).Equals(".exe", StringComparison.OrdinalIgnoreCase))
            throw new IOException("The declared executable is outside the package or is not an executable.");
        for (var current = new FileInfo(path) as FileSystemInfo; current is not null; current = current is FileInfo file ? file.Directory : ((DirectoryInfo)current).Parent)
            if (current.Exists && (current.Attributes & FileAttributes.ReparsePoint) != 0)
                throw new IOException("Linked executable paths are not supported.");
        return path;
    }
}
