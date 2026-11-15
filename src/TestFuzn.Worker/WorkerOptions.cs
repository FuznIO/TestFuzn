using System.Text.RegularExpressions;

namespace Fuzn.TestFuzn.Worker;

internal partial class WorkerOptions
{
    public string Pool { get; private init; } = "";
    public string WorkDirectory { get; private init; } = "";

    public static WorkerOptions Parse(string[] args)
    {
        string? pool = null;
        string? workDirectory = null;

        for (var index = 0; index < args.Length; index++)
        {
            var (name, value) = Split(args[index]);
            if (value == null && index + 1 < args.Length && !args[index + 1].StartsWith("--"))
                value = args[++index];

            switch (name)
            {
                case "--pool":
                    pool = value;
                    break;
                case "--work-dir":
                    workDirectory = value;
                    break;
                default:
                    throw new ArgumentException($"Unknown argument '{args[index]}'.");
            }
        }

        if (string.IsNullOrWhiteSpace(pool))
            throw new ArgumentException("--pool is required.");
        if (!SubjectToken().IsMatch(pool))
            throw new ArgumentException($"Pool name '{pool}' may only contain letters, digits, '-' and '_'.");

        if (string.IsNullOrWhiteSpace(workDirectory))
            workDirectory = Path.Combine(Path.GetTempPath(), "testfuzn-worker");

        return new WorkerOptions
        {
            Pool = pool,
            WorkDirectory = Path.GetFullPath(workDirectory)
        };
    }

    private static (string Name, string? Value) Split(string arg)
    {
        var parts = arg.Split('=', 2);
        return parts.Length == 2 ? (parts[0], parts[1]) : (parts[0], null);
    }

    // Pool names and worker ids go into Cockpit's URLs and messages, so they stay plain.
    [GeneratedRegex("^[A-Za-z0-9_-]+$")]
    internal static partial Regex SubjectToken();
}
