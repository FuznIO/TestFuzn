namespace Fuzn.TestFuzn.Adapters;

internal class BasicMsTestContext : TestContext
{
    private string _testName;

    public BasicMsTestContext(string testName)
    {
        _testName = testName;
    }

    public override IDictionary<string, object> Properties => throw new NotImplementedException();

    public override string TestName => _testName;

    public override void AddResultFile(string fileName)
    {
        throw new NotImplementedException();
    }

    public override void DisplayMessage(MessageLevel messageLevel, string message)
    {
        throw new NotImplementedException();
    }

    public override void Write(string? message)
    {
        Console.Write(message);
    }

    public override void Write(string format, params object?[] args)
    {
        Console.Write(format, args);
    }

    public override void WriteLine(string? message)
    {
        Console.WriteLine(message);
    }

    public override void WriteLine(string format, params object?[] args)
    {
        Console.WriteLine(format, args);
    }
}
