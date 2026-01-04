namespace Fuzn.TestFuzn;

/// <summary>
/// Specifies the group name for a test class.
/// Tests in the same group are reported together in test results.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public class GroupAttribute : Attribute
{
    /// <summary>
    /// Gets the name of the group.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="GroupAttribute"/> class.
    /// </summary>
    /// <param name="name">The name of the group.</param>
    public GroupAttribute(string name)
    {
        Name = name;
    }
}
