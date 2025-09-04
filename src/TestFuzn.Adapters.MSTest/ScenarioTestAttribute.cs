using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Fuzn.TestFuzn;

[AttributeUsage(AttributeTargets.Method)]
public class ScenarioTestAttribute : TestMethodAttribute
{
    public ScenarioTestAttribute()
    {
    }

    public ScenarioTestAttribute(string? displayName) : base(displayName)
    {
    }
}
