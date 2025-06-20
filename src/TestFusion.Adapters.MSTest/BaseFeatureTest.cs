using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Runtime.CompilerServices;

namespace TestFusion;

public abstract class BaseFeatureTest : IFeatureTest
{
    public TestContext TestContext { get; set; }
    public virtual string FeatureName
    {
        get
        {
            var featureName = GetType().Name.Replace('_', ' ');
            if (featureName.EndsWith("Tests"))
                return featureName.Substring(0, featureName.Length - 5);
            if (featureName.EndsWith("Test"))
                return featureName.Substring(0, featureName.Length - 4);
            return featureName;
        }
    }

    public ScenarioBuilder<StepContext> Scenario([CallerMemberName] string scenarioName = null)
    {
        var scenario = new ScenarioBuilder<StepContext>(new MsTestAdapter(TestContext), this, scenarioName);
        return scenario;
    }

    public ScenarioBuilder<TStepContext> Scenario<TStepContext>([CallerMemberName] string scenarioName = null)
        where TStepContext : StepContext, new()
    {
        var scenario = new ScenarioBuilder<TStepContext>(new MsTestAdapter(TestContext), this, scenarioName);
        return scenario;
    }

    public virtual Task InitScenarioTest(Context context)
    {
        return Task.CompletedTask;
    }

    public virtual Task CleanupScenarioTest(Context context)
    {
        return Task.CompletedTask;
    }
}
