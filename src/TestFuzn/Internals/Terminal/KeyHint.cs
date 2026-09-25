namespace Fuzn.TestFuzn.Internals.Terminal;

internal readonly struct KeyHint
{
    public string Key { get; }

    public string Description { get; }

    public KeyHint(string key, string description)
    {
        if (key == null)
            throw new ArgumentNullException(nameof(key), "Key cannot be null.");
        if (description == null)
            throw new ArgumentNullException(nameof(description), "Description cannot be null.");

        Key = key;
        Description = description;
    }
}
