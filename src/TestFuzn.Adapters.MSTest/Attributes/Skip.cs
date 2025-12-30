namespace Fuzn.TestFuzn;

[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
public class SkipAttribute : Attribute
{
    public string Reason { get; set; }
    public SkipAttribute(string reason = "")
    {
        Reason = reason;
    }
}
