namespace Fuzn.TestFuzn.Tests.Steps;

public class CustomModel
{
    private static readonly AsyncLocal<string?> _asyncLocalCustomProperty = new();

    public string CustomProperty { get; set; }

    public string? AsyncLocalCustomProperty
    {
        get => _asyncLocalCustomProperty.Value;
        set => _asyncLocalCustomProperty.Value = value;
    }
}   
