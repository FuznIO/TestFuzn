namespace Fuzn.TestFuzn.Tests.ConsoleOutput;

/// <summary>
/// A <see cref="TestContext"/> that records everything written through it, so the real
/// <see cref="MsTestRunnerAdapter"/> can be driven in a hermetic test: the MSTest path's output
/// is what this collects, line for line.
/// </summary>
internal sealed class RecordingTestContext : TestContext
{
    /// <summary>
    /// Everything written through the context, in order: one entry per call, holding exactly the
    /// text that call was given — a <see cref="WriteLine(string?)"/> without the line break it
    /// adds. An entry is a write, not necessarily a line: the same MSTest path also writes markup
    /// that ends in its own line break and hands whole multi-line sections to a single call, so
    /// only a write the caller made as one line is one line here.
    /// </summary>
    public List<string> Lines { get; } = new List<string>();

    public override IDictionary<string, object?> Properties { get; } = new Dictionary<string, object?>();

    public override void AddResultFile(string fileName)
    {
    }

    public override void DisplayMessage(MessageLevel messageLevel, string message)
    {
    }

    public override void Write(string? message)
    {
        if (message == null)
            Lines.Add(string.Empty);
        else
            Lines.Add(message);
    }

    public override void Write(string format, params object?[] args)
    {
        Lines.Add(string.Format(format, args));
    }

    public override void WriteLine(string? message)
    {
        if (message == null)
            Lines.Add(string.Empty);
        else
            Lines.Add(message);
    }

    public override void WriteLine(string format, params object?[] args)
    {
        Lines.Add(string.Format(format, args));
    }
}
