using System.Globalization;

namespace Fuzn.TestFuzn.Internals;

/// <summary>
/// Parses the runner's command-line arguments into a case-insensitive key/value dictionary.
/// Two forms are recognized, both prefixed <c>--</c>: a key/value pair <c>--key=value</c> (the
/// value trimmed and stripped of surrounding quotes) and a bare boolean flag <c>--flag</c>, which
/// is recorded with the value <see cref="FlagValue"/> so that a lookup finds it and
/// <see cref="HasFlag"/> reads it. Anything else — the <c>run</c> verb, single-dash options, a
/// lone <c>--</c> — is ignored. The parser knows no schema: every <c>--</c> argument is recorded,
/// whether or not anything reads it, so an unknown flag is harmless; a key given more than once
/// keeps its last value, so <c>--demo --demo=false</c> ends up not set. Reading a recorded value
/// as anything but text is the reader's: <see cref="HasFlag"/> for a boolean flag and
/// <see cref="TryGetDuration"/> for a duration in whole seconds, both of which say what an
/// unreadable value means so the caller can report the invocation error.
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

    /// <summary>
    /// Reads a duration argument given in whole seconds (<c>--demo-duration=45</c>): true with
    /// <paramref name="duration"/> set — <paramref name="defaultDuration"/> when the argument is
    /// absent, so an unset argument is not an error — and false when it is given but is not a
    /// whole number of seconds of at least <paramref name="minimum"/>: a bare flag, a fraction,
    /// a number too large for an <see cref="int"/>, text, or a value below the minimum. The
    /// caller reports a false as an invocation error; <paramref name="duration"/> is the default
    /// then, never a half-read value. The number is read culture-independently, so the same
    /// command line means the same thing on every machine.
    /// </summary>
    public static bool TryGetDuration(Dictionary<string, string>? parsedArgs, string argsKey, TimeSpan minimum, TimeSpan defaultDuration, out TimeSpan duration)
    {
        duration = defaultDuration;
        if (parsedArgs == null || !parsedArgs.TryGetValue(argsKey, out var value))
            return true;

        if (value == null)
            return false;

        if (!int.TryParse(value.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var seconds))
            return false;

        var parsedDuration = TimeSpan.FromSeconds(seconds);
        if (parsedDuration < minimum)
            return false;

        duration = parsedDuration;
        return true;
    }

    public string GetValueFromArgsOrEnvironmentVariable(Dictionary<string, string>? parsedArgs, string argsKey, string envKey)
    {
        // First try to get from command arguments
        if (parsedArgs != null && parsedArgs.TryGetValue(argsKey, out var value))
            return value;

        return _environmentWrapper.GetEnvironmentVariable(envKey) ?? "";
    }
}
