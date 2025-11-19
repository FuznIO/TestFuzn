namespace Fuzn.TestFuzn;

[AttributeUsage(AttributeTargets.Method)]
public class EnvironmentsAttribute : Attribute
{
    public string[] Environments { get; internal set; }

    public EnvironmentsAttribute(params string[] environments)
    {
        Environments = environments;
    }
}