using System.IO.Compression;
using Fuzn.TestFuzn.Internals.Cluster.Protocol;

namespace Fuzn.TestFuzn.Internals.Cluster.Remote;

// The build goes to Cockpit once per hash; workers download it by hash. Only folders of the
// workers' OS family are kept under runtimes/ and .playwright/node/ (every CPU type of that family).
internal static class BuildPackager
{
    private static readonly string[] SkippedTopFolders = ["TestFuznResults", "TestResults", "Log", "Logs"];

    public static List<string> Files(string directory, string osFamily)
    {
        var root = Path.GetFullPath(directory);
        var files = new List<string>();
        foreach (var path in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(root, path).Replace('\\', '/');
            if (IsIncluded(relative, osFamily))
                files.Add(relative);
        }

        files.Sort(StringComparer.Ordinal);
        return files;
    }

    internal static bool IsIncluded(string relativePath, string osFamily)
    {
        var segments = relativePath.Split('/');
        if (segments.Length > 1 && SkippedTopFolders.Contains(segments[0], StringComparer.OrdinalIgnoreCase))
            return false;

        for (var index = 0; index < segments.Length - 1; index++)
        {
            if (segments[index].Equals("runtimes", StringComparison.OrdinalIgnoreCase) && index + 2 < segments.Length)
                return RuntimeFolderMatches(segments[index + 1], osFamily);

            if (segments[index].Equals(".playwright", StringComparison.OrdinalIgnoreCase)
                && index + 3 < segments.Length
                && segments[index + 1].Equals("node", StringComparison.OrdinalIgnoreCase))
                return PlaywrightNodeFolderMatches(segments[index + 2], osFamily);
        }

        return true;
    }

    internal static bool RuntimeFolderMatches(string folder, string osFamily)
    {
        var name = folder.ToLowerInvariant();
        if (name.StartsWith("win"))
            return osFamily == OsFamilies.Windows;
        if (name.StartsWith("linux"))
            return osFamily == OsFamilies.Linux;
        if (name.StartsWith("osx"))
            return osFamily == OsFamilies.MacOs;
        if (name.StartsWith("unix"))
            return osFamily != OsFamilies.Windows;

        return true;
    }

    internal static bool PlaywrightNodeFolderMatches(string folder, string osFamily)
    {
        var name = folder.ToLowerInvariant();
        if (name.StartsWith("win"))
            return osFamily == OsFamilies.Windows;
        if (name.StartsWith("linux"))
            return osFamily == OsFamilies.Linux;
        if (name.StartsWith("darwin") || name.StartsWith("mac"))
            return osFamily == OsFamilies.MacOs;

        return true;
    }

    // From paths and contents, not zip bytes, so the same build always gets the same hash.
    public static string Hash(string directory, IReadOnlyList<string> files) => BuildHash.Of(directory, files);

    public static void Zip(string directory, IReadOnlyList<string> files, Stream output)
    {
        using var archive = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true);
        foreach (var file in files)
            archive.CreateEntryFromFile(Path.Combine(directory, file), file, CompressionLevel.Fastest);
    }

    public static async Task<bool> UploadIfMissing(CockpitConnection cockpit, string directory, IReadOnlyList<string> files, string hash, CancellationToken cancellationToken)
    {
        if (await cockpit.Exists(CockpitPaths.Build(hash), cancellationToken))
            return false;

        var zipPath = Path.Combine(Path.GetTempPath(), $"testfuzn-build-{hash}.zip");
        try
        {
            await using (var zip = File.Create(zipPath))
                Zip(directory, files, zip);

            await using var upload = File.OpenRead(zipPath);
            await cockpit.Put(CockpitPaths.Build(hash), upload, cancellationToken);
            return true;
        }
        finally
        {
            File.Delete(zipPath);
        }
    }
}
