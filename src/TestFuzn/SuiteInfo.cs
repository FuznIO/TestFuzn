namespace Fuzn.TestFuzn;

/// <summary>
/// Contains information about a test suite.
/// </summary>
public class SuiteInfo
{
    /// <summary>
    /// Gets or sets the name of the test suite.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the unique identifier for the test suite.
    /// Used to correlate results across different test runs if Name changes.
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// Gets or sets the metadata key-value pairs associated with the test suite.
    /// </summary>
    public Dictionary<string, string> Metadata { get; set; }
}