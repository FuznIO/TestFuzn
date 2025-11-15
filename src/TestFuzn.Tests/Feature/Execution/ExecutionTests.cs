namespace Fuzn.TestFuzn.Tests.Feature.Init;

[FeatureTest]
public class ExecutionTests : BaseFeatureTest
{
    public override string FeatureName => "Feature-Execution";

    [ScenarioTest]
    public async Task Verify_Run_Once()
    {
        var executionCount = 0;

        await Scenario("Verify that a feature scenario without simulations is run only once")
            .Step("Step 1", (context) => 
            {
                Interlocked.Increment(ref executionCount);
            })
            .Run();

        Assert.AreEqual(1, executionCount);
    }
}
