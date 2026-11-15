using System.IO.Compression;

namespace Fuzn.TestFuzn.Internals.Cluster.Protocol;

internal static class SafeZip
{
    // Rejects entries with ".." or absolute paths ("zip slip"), so a build or report can only write
    // inside the target folder.
    public static void Extract(Stream zipStream, string targetDirectory, bool setExecuteBit)
    {
        var root = Path.GetFullPath(targetDirectory);
        if (!root.EndsWith(Path.DirectorySeparatorChar))
            root += Path.DirectorySeparatorChar;

        Directory.CreateDirectory(root);

        using var archive = new ZipArchive(zipStream, ZipArchiveMode.Read);
        foreach (var entry in archive.Entries)
        {
            var name = entry.FullName.Replace('\\', '/');
            if (name.Length == 0)
                continue;

            if (Path.IsPathRooted(name) || name.Split('/').Contains(".."))
                throw new InvalidDataException($"The archive contains an unsafe path: '{entry.FullName}'.");

            var destination = Path.GetFullPath(Path.Combine(root, name));
            if (!destination.StartsWith(root, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
                throw new InvalidDataException($"The archive contains an unsafe path: '{entry.FullName}'.");

            if (name.EndsWith('/'))
            {
                Directory.CreateDirectory(destination);
                continue;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            entry.ExtractToFile(destination, overwrite: true);

            // Files zipped on Windows carry no execute bit, and e.g. Playwright's node binary needs it.
            if (setExecuteBit && !OperatingSystem.IsWindows())
                File.SetUnixFileMode(destination, File.GetUnixFileMode(destination) | UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute);
        }
    }
}
