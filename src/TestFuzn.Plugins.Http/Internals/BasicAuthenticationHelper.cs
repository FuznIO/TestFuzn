using System.Text;

namespace FuznLabs.TestFuzn.Plugins.Http.Internals;

internal static class BasicAuthenticationHelper
{
    internal static string ToBase64String(string username, string password)
    {
        return Convert.ToBase64String(Encoding.UTF8.GetBytes($"{username}:{password}"));
    }
}