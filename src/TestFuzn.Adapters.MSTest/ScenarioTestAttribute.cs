namespace Fuzn.TestFuzn;

[AttributeUsage(AttributeTargets.Method)]
public class ScenarioTestAttribute : TestMethodAttribute
{
    public ScenarioRunMode RunMode { get; internal set; }

    public ScenarioTestAttribute(ScenarioRunMode runMode = ScenarioRunMode.Execute)
    {
        RunMode = runMode;
    }

    public override async Task<TestResult[]> ExecuteAsync(ITestMethod testMethod)
    {
        var result = await base.ExecuteAsync(testMethod);
        return result;
    }
}
