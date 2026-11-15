using System.Text.RegularExpressions;

namespace Fuzn.TestFuzn.Internals.Results.Load;

internal static partial class ErrorShape
{
    public const string OtherErrors = "Other errors";
    public const string OtherErrorsKey = "\u001fother";
    public const string OtherErrorsDetails = "More kinds of error than the report keeps for one step. They are counted here, without their messages.";

    public static string Of(string message)
    {
        if (string.IsNullOrEmpty(message))
            return "";

        var shape = Guid().Replace(message, "{id}");
        shape = HexId().Replace(shape, "{id}");
        shape = Decimal().Replace(shape, "{n}");
        shape = LongNumber().Replace(shape, "{n}");
        return shape;
    }

    [GeneratedRegex(@"\b[0-9a-fA-F]{8}-?[0-9a-fA-F]{4}-?[0-9a-fA-F]{4}-?[0-9a-fA-F]{4}-?[0-9a-fA-F]{12}\b")]
    private static partial Regex Guid();

    [GeneratedRegex(@"\b(?=[0-9a-fA-F]*\d)(?=[0-9a-fA-F]*[a-fA-F])[0-9a-fA-F]{8,}\b")]
    private static partial Regex HexId();

    [GeneratedRegex(@"\d+[.,]\d+")]
    private static partial Regex Decimal();

    [GeneratedRegex(@"\d{4,}")]
    private static partial Regex LongNumber();
}
