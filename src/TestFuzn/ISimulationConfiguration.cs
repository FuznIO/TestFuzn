namespace FuznLabs.TestFuzn;

public interface ILoadConfiguration
{
    public bool IsWarmup { get; set; }
    public string GetDescription();
}
