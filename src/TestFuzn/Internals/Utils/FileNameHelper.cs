using System.Text.RegularExpressions;

namespace Fuzn.TestFuzn.Internals.Utils;

internal class FileNameHelper
{
    public static string MakeFilenameSafe(string input)
    {
        // Get all invalid filename characters
        var invalidChars = Path.GetInvalidFileNameChars();
        var invalidCharsPattern = $"[{Regex.Escape(new string(invalidChars))}]";

        // Replace invalid characters with underscore (_)
        var safe = Regex.Replace(input, invalidCharsPattern, "_");

        // Optionally trim spaces and dots at the end
        return safe.Trim().TrimEnd('.');
    }
}
