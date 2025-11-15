using Fuzn.TestFuzn;

namespace SampleApp.Tests.FeatureTests;

[FeatureTest]
public class UpdateProductE2ETests : BaseFeatureTest
{
    public override string FeatureName => "Update product";

    [ScenarioTest]
    public async Task Update()
    {
        await Scenario("Verify that update product works")
            .Step("Create a product", context => { })
            .Step("Call the update API-endpoint to update the product", context => { })
            .Step("Call get /Product and ensure it has been updated", context => { })
            .Run();
    }
}
