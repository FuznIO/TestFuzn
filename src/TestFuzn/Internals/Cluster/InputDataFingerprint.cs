using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Fuzn.TestFuzn.Internals.Cluster;

// Every load worker loads the input data list itself. The controller compares count and hash, so an
// unordered source (e.g. a database query) that gives workers different lists shows up as a warning.
internal static class InputDataFingerprint
{
    public static string Hash(IReadOnlyList<object> items)
    {
        using var sha = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach (var item in items)
        {
            sha.AppendData(Encoding.UTF8.GetBytes(Describe(item)));
            sha.AppendData([0]);
        }

        return Convert.ToHexString(sha.GetHashAndReset()).Substring(0, 12).ToLowerInvariant();
    }

    private static string Describe(object? item)
    {
        if (item == null)
            return "null";

        try
        {
            return JsonSerializer.Serialize(item, item.GetType(), ClusterJson.Options);
        }
        catch (Exception)
        {
            var text = item.ToString();
            if (text == null)
                return "";

            return text;
        }
    }
}
