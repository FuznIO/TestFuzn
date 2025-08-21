using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FuznLabs.TestFuzn;

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
