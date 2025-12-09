namespace Fuzn.TestFuzn.Internals;

internal interface ILoadConfiguration
{
    public bool IsWarmup { get; set; }
    public string GetDescription();
}
