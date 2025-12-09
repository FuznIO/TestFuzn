namespace Fuzn.TestFuzn.Internals;

internal static class ArgumentsParser
{
    public static Dictionary<string, string> Parse(string[] args)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    
        if (args == null || args.Length == 0)
            return result;

        foreach (var arg in args)
        {
            if (!arg.StartsWith("--"))
                continue;

            var parts = arg.Split('=', 2);
            if (parts.Length != 2)
                continue;

            var key = parts[0].Substring(2);
            var value = parts[1].Trim().Trim('\'', '"');
            result[key] = value;
        }

        return result;
    }

    public static string GetValueFromArgsOrEnvironmentVariable(Dictionary<string, string> parsedArgs, string argsKey, string envKey)
    {
        // First try to get from command arguments
        if (parsedArgs != null && parsedArgs.TryGetValue(argsKey, out var value))
            return value;

        return Environment.GetEnvironmentVariable(envKey) ?? "";
    }
}
