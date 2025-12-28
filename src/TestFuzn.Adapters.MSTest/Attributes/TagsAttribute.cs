namespace Fuzn.TestFuzn;

[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
public class TagsAttribute : TestCategoryBaseAttribute, ITagsAttribute
{
    public override IList<string> TestCategories => Tags;

    public List<string> Tags { get; private set; }

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
