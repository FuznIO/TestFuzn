using System.Text.RegularExpressions;

namespace Fuzn.TestFuzn.ConsoleOutput;

internal class MarkupHelper
{
    internal static string StripMarkup(string input)
    {
        return Regex.Replace(input ?? string.Empty, @"\[[^\]]+\]", string.Empty);
    }
}
