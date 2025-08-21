using FuznLabs.TestFuzn;

namespace SampleApp.Tests.FeatureTests;

[FeatureTest]
public class CreateProductE2ETests : BaseFeatureTest
{
    public override string FeatureName => "Create product";

    [ScenarioTest]
    public async Task Update()
    {
        await Scenario("Verify that create product works")
            .Run();
    }
}
