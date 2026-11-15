using System.Security.Cryptography;
using System.Text;

namespace Fuzn.TestFuzn.Internals;

internal static class StableId
{
    private const int Length = 12;
    private const char Separator = '\u001f';

    public static string Of(string value)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(hash, 0, Length / 2).ToLowerInvariant();
    }

    public static string Of(string parentId, string name)
    {
        return Of(parentId + Separator + name);
    }

    public static string Key(string parentId, string name)
    {
        return parentId + Separator + name;
    }
}
