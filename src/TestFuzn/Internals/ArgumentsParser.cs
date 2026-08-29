namespace Fuzn.TestFuzn.Internals;

/// <summary>
/// Parses the runner's command-line arguments into a case-insensitive key/value dictionary.
/// Two forms are recognized, both prefixed <c>--</c>: a key/value pair <c>--key=value</c> (the
/// value trimmed and stripped of surrounding quotes) and a bare boolean flag <c>--flag</c>, which
/// is recorded with the value <see cref="FlagValue"/> so that a lookup finds it and
/// <see cref="HasFlag"/> reads it. Anything else — the <c>run</c> verb, single-dash options, a
/// lone <c>--</c> — is ignored. The parser knows no schema: every <c>--</c> argument is recorded,
/// whether or not anything reads it, so an unknown flag is harmless; a key given more than once
/// keeps its last value, so <c>--demo --demo=false</c> ends up not set.
/// </summary>
internal class ArgumentsParser
{
    /// <summary>The value a bare flag (<c>--demo</c>) is recorded with.</summary>
    public const string FlagValue = "true";

    private const string ArgumentPrefix = "--";

    private readonly IEnvironmentWrapper _environmentWrapper;

    public ArgumentsParser(IEnvironmentWrapper environmentWrapper)
    {
        _environmentWrapper = environmentWrapper;
    }

    public Dictionary<string, string> Parse(string[] args)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (args == null || args.Length == 0)
            return result;

        foreach (var arg in args)
        {
            if (!arg.StartsWith(ArgumentPrefix))
                continue;

            var parts = arg.Split('=', 2);
            if (parts.Length != 2)
            {
                // A bare flag: no value given, recorded as set. A lone "--" names nothing.
                var flag = parts[0].Substring(ArgumentPrefix.Length);
                if (flag.Length == 0)
                    continue;

                result[flag] = FlagValue;
                continue;
            }

            var key = parts[0].Substring(ArgumentPrefix.Length);
            var value = parts[1].Trim().Trim('\'', '"');
            result[key] = value;
        }

        return result;
    }

    /// <summary>
    /// Whether a boolean flag is set in the parsed arguments: given bare (<c>--demo</c>) or with
    /// any value other than <c>false</c> or <c>0</c>, compared ignoring case (<c>--demo=true</c>,
    /// <c>--demo=yes</c>); an absent flag, <c>--demo=false</c> and <c>--demo=0</c> are not set.
    /// Null parsed arguments hold no flag.
    /// </summary>
    public static bool HasFlag(Dictionary<string, string>? parsedArgs, string argsKey)
    {
        if (parsedArgs == null || !parsedArgs.TryGetValue(argsKey, out var value))
            return false;

        if (value == null)
            return false;

        var trimmed = value.Trim();
        return !trimmed.Equals("false", StringComparison.OrdinalIgnoreCase) && trimmed != "0";
    }

    public string GetValueFromArgsOrEnvironmentVariable(Dictionary<string, string>? parsedArgs, string argsKey, string envKey)
    {
        // First try to get from command arguments
        if (parsedArgs != null && parsedArgs.TryGetValue(argsKey, out var value))
            return value;

        return _environmentWrapper.GetEnvironmentVariable(envKey) ?? "";
    }
}
