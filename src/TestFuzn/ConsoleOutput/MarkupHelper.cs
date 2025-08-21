using System.Text.RegularExpressions;

namespace FuznLabs.TestFuzn.ConsoleOutput;

internal class MarkupHelper
{
    internal static string StripMarkup(string input)
    {
        return Regex.Replace(input ?? string.Empty, @"\[[^\]]+\]", string.Empty);
    }
}
