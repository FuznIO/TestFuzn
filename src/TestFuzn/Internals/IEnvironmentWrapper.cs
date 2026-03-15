namespace Fuzn.TestFuzn.Internals;

internal interface IEnvironmentWrapper
{
    string? GetEnvironmentVariable(string key);
    string GetMachineName();
}
