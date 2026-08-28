namespace Fuzn.TestFuzn.Internals.Terminal;

/// <summary>
/// One key-to-action pair for <see cref="KeyHintBarWidget"/>. Both parts are markup strings.
/// </summary>
internal readonly struct KeyHint
{
    /// <summary>The key the user presses, e.g. "q" or "ctrl+c".</summary>
    public string Key { get; }

    /// <summary>What the key does, e.g. "quit".</summary>
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
