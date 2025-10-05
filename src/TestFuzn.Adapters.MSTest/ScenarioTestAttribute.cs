using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Fuzn.TestFuzn;

[AttributeUsage(AttributeTargets.Method)]
public class ScenarioTestAttribute : TestMethodAttribute
{
    public ScenarioRunMode RunMode { get; internal set; }

    public ScenarioTestAttribute(ScenarioRunMode runMode = ScenarioRunMode.Execute)
    {
        RunMode = runMode;
    }
}
