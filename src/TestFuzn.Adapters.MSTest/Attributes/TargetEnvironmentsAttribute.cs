namespace Fuzn.TestFuzn;

/// <summary>
/// Specifies which target environments (e.g., Dev, Test, Staging, Production) 
/// this test is allowed to run against.
/// </summary>
/// <remarks>
/// The current environment is determined by the <c>TESTFUZN_TARGET_ENVIRONMENT</c> environment variable
/// or the <c>--target-environment</c> command-line argument. Environment names are case-insensitive.
/// </remarks>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
public class TargetEnvironmentsAttribute : Attribute
{
    public string[] Environments { get; }

    public TargetEnvironmentsAttribute(params string[] environments)
    {
        Environments = environments ?? [];
    }
}
