using System.Security.Cryptography;
using System.Text;

namespace Fuzn.TestFuzn.Internals.Cluster.Protocol;

// A build's name is the hash of its file paths and contents (not the zip bytes), so the same build always
// gets the same name. The worker computes it again after unpacking and refuses a build that does not match,
// so a build can never run under another build's name.
internal static class BuildHash
{
    public static string Of(string directory, IReadOnlyList<string> files)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach (var file in files)
        {
            hash.AppendData(Encoding.UTF8.GetBytes(file));
            hash.AppendData([0]);
            using var stream = File.OpenRead(Path.Combine(directory, file));
            hash.AppendData(SHA256.HashData(stream));
        }

        return Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
    }

    // Every file under the folder, as relative paths with '/', in the order the client hashed them.
    public static List<string> AllFiles(string directory)
    {
        var root = Path.GetFullPath(directory);
        var files = Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(root, path).Replace('\\', '/'))
            .ToList();
        files.Sort(StringComparer.Ordinal);
        return files;
    }

    public static bool IsHash(string value) => value.Length == 64 && value.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f');
}
