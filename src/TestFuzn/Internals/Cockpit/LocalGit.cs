using System.Diagnostics;

namespace Fuzn.TestFuzn.Internals.Cockpit;

internal static class LocalGit
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);
    private static readonly Lazy<(string? Branch, string? Commit)> Head = new(ReadHead);

    public static string? Branch => Head.Value.Branch;
    public static string? Commit => Head.Value.Commit;

    private static (string? Branch, string? Commit) ReadHead()
    {
        try
        {
            var startInfo = new ProcessStartInfo("git")
            {
                WorkingDirectory = AppContext.BaseDirectory,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            foreach (var argument in new[] { "rev-parse", "HEAD", "--abbrev-ref", "HEAD" })
                startInfo.ArgumentList.Add(argument);

            using var process = Process.Start(startInfo);
            if (process == null)
                return (null, null);

            var output = process.StandardOutput.ReadToEndAsync();
            _ = process.StandardError.ReadToEndAsync();
            if (!process.WaitForExit(Timeout))
            {
                process.Kill(entireProcessTree: true);
                return (null, null);
            }
            if (process.ExitCode != 0)
                return (null, null);

            var lines = output.Result.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (lines.Length != 2)
                return (null, null);

            var branch = lines[1] == "HEAD" ? null : lines[1];
            return (branch, lines[0]);
        }
        catch (Exception)
        {
            return (null, null);
        }
    }
}
