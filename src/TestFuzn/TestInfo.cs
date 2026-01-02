namespace Fuzn.TestFuzn;

/// <summary>
/// Contains metadata and configuration information for a test.
/// </summary>
public class TestInfo
{
    /// <summary>
    /// Gets or sets the name of the test.
    /// </summary>
    internal string Name { get; set; }

    /// <summary>
    /// Gets or sets the fully qualified name of the test.
    /// </summary>
    internal string FullName { get; set; }

    /// <summary>
    /// Gets or sets the unique identifier for the test.
    /// Used to correlate results across different test runs if Name changes.
    /// </summary>
    internal string Id { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the test is skipped.
    /// </summary>
    internal bool Skipped { get; set; }

    /// <summary>
    /// Gets or sets the reason the test was skipped.
    /// </summary>
    public string SkipReason { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the test has a Skip attribute.
    /// </summary>
    internal bool HasSkipAttribute { get; set; }

    /// <summary>
    /// Gets or sets the reason specified in the Skip attribute.
    /// </summary>
    internal string SkipAttributeReason { get; set; }

    /// <summary>
    /// Gets or sets the description of the test.
    /// </summary>
    internal string Description { get; set; }

    /// <summary>
    /// Gets or sets the group information for the test.
    /// </summary>
    internal GroupInfo Group { get; set; }

    /// <summary>
    /// Gets or sets the metadata key-value pairs associated with the test.
    /// </summary>
    internal Dictionary<string, string> Metadata { get; set; }

    /// <summary>
    /// Gets or sets the tags associated with the test for filtering.
    /// </summary>
    internal List<string> Tags { get; set; }

    /// <summary>
    /// Gets or sets the target environments the test is allowed to run against.
    /// </summary>
    internal List<string> TargetEnvironments { get; set; }
}
