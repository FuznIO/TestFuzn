namespace Fuzn.TestFuzn;

/// <summary>
/// Specifies which target environments (e.g., Dev, Test, Staging, Production) 
/// this test is allowed to run against.
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
public class TargetEnvironmentsAttribute : Attribute
{
    public string[] Environments { get; }

    public TargetEnvironmentsAttribute(params string[] environments)
    {
        Environments = environments ?? [];
    }
}
