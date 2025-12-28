namespace Fuzn.TestFuzn;

[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
public class EnvironmentsAttribute : Attribute
{
    public string[] Environments { get; internal set; }

    public EnvironmentsAttribute(params string[] environments)
    {
        Environments = environments;
    }
}