namespace Fuzn.TestFuzn.Internals;

internal class EnvironmentWrapper : IEnvironmentWrapper
{
    public string? GetEnvironmentVariable(string key)
    {
        return Environment.GetEnvironmentVariable(key);
    }

    public string GetMachineName()
    {
        return Environment.MachineName;
    }
}
