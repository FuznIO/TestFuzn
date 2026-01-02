namespace Fuzn.TestFuzn;

/// <summary>
/// Specifies tags for a test method or class for categorization and filtering.
/// Tags can be used to filter which tests to run using the <see cref="GlobalState.TagsFilterInclude"/> 
/// and <see cref="GlobalState.TagsFilterExclude"/> properties.
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
public class TagsAttribute : TestCategoryBaseAttribute
{
    /// <inheritdoc/>
    public override IList<string> TestCategories => Tags;

    /// <summary>
    /// Gets the list of tags associated with the test.
    /// </summary>
    public List<string> Tags { get; private set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="TagsAttribute"/> class with the specified tags.
    /// </summary>
    /// <param name="tag">One or more tags to associate with the test.</param>
    /// <exception cref="ArgumentException">Thrown when no tags are specified or a tag is null/whitespace.</exception>
    public TagsAttribute(params string[] tag)
    {
        if (tag == null || tag.Length == 0)
            throw new ArgumentException("At least one tag must be specified.", nameof(tag));

        foreach (var t in tag)
        {
            if (string.IsNullOrWhiteSpace(t))
                throw new ArgumentException("Tags cannot be null or whitespace.", nameof(tag));
        }

        Tags = tag.ToList();
    }
}
