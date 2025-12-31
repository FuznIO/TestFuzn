namespace Fuzn.TestFuzn;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public class GroupAttribute : Attribute
    {
    public string Name { get; }

    public GroupAttribute(string name)
    {
        Name = name;
    }
}
