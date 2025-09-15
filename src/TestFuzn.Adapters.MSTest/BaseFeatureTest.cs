using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Runtime.CompilerServices;

namespace Fuzn.TestFuzn;

public abstract class BaseFeatureTest : IFeatureTest
{
    public TestContext TestContext { get; set; }
    public virtual string FeatureName { get; set; }
    public virtual string FeatureId { get; set; }
    public virtual Dictionary<string, string> FeatureMetadata { get; set; } = new();

    protected BaseFeatureTest()
    {
        var featureName = GetType().Name.Replace('_', ' ');
        if (featureName.EndsWith("Tests"))
            featureName = featureName.Substring(0, featureName.Length - 5);
        else if (featureName.EndsWith("Test"))
            featureName = featureName.Substring(0, featureName.Length - 4);
            
        FeatureName = featureName;
    }

    public ScenarioBuilder<StepContext<EmptyCustomStepContext>> Scenario([CallerMemberName] string scenarioName = null)
    {
        var scenario = new ScenarioBuilder<StepContext<EmptyCustomStepContext>>(new MsTestAdapter(TestContext), this, scenarioName);
        return scenario;
    }

    public ScenarioBuilder<StepContext<TCustomContext>> Scenario<TCustomContext>([CallerMemberName] string scenarioName = null)
        where TCustomContext : new()
    {
        var scenario = new ScenarioBuilder<StepContext<TCustomContext>>(new MsTestAdapter(TestContext), this, scenarioName);
        return scenario;
    }

    public virtual Task InitTestMethod(Context context)
    {
        return Task.CompletedTask;
    }

    public virtual Task CleanupTestMethod(Context context)
    {
        return Task.CompletedTask;
    }
}
