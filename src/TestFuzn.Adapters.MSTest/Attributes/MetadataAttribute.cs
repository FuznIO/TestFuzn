namespace Fuzn.TestFuzn;

/// <summary>
/// Specifies a key-value metadata pair for a test method or class.
/// Multiple metadata attributes can be applied to the same test.
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = true, Inherited = true)]
public class MetadataAttribute : Attribute
{
    /// <summary>
    /// Gets the metadata key.
    /// </summary>
    public string Key { get; }

    /// <summary>
    /// Gets the metadata value.
    /// </summary>
    public string Value { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="MetadataAttribute"/> class.
    /// </summary>
    /// <param name="key">The metadata key.</param>
    /// <param name="value">The metadata value.</param>
    public MetadataAttribute(string key, string value)
    {
        Key = key;
        Value = value;
    }
}
