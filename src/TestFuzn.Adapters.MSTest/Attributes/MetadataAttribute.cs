namespace Fuzn.TestFuzn;

[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = true, Inherited = true)]
public class MetadataAttribute : Attribute
{
    public string Key { get; }
    public string Value { get; }

    public MetadataAttribute(string key, string value)
    {
        Key = key;
        Value = value;
    }
}
