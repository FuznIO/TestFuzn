namespace Fuzn.TestFuzn;

/// <summary>
/// Ordered collection of string key-value pairs with dictionary-style initializer syntax.
/// Used for report metadata and structured input data output where lookup is not required.
/// </summary>
public class KeyValueList : List<KeyValuePair<string, string>>
{
    public void Add(string key, string value) => Add(new KeyValuePair<string, string>(key, value));
}
