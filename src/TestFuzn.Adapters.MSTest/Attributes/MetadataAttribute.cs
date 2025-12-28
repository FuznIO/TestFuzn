namespace Fuzn.TestFuzn;

[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = true, Inherited = true)]
public class MetadataAttribute : Attribute
{
    public MetadataAttribute(string key, string value)
    {
    }
}
