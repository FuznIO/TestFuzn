namespace TestFusion.Tests.Reports;

[FeatureTest]
public class FeatureReportTests : BaseFeatureTest
{
    [ScenarioTest]
    public async Task Feature1()
    {
        await Scenario()
            .InputData("user1", "user2", "user")
            .Step("Test Step 1", (context) =>
            {
            })
            .Step("Test Step 2", (context) =>
            {
            })
            .Step("Test Step 3", (context) =>
            {
            })
            .Step("Test Step 4", (context) =>
            {
            })
            .Step("Test Step 5", (context) =>
            {
            })
            .Run();
    }
    [ScenarioTest]
    public async Task Feature2()
    {
        await Scenario()
            .Step("Test Step 1", (context) =>
            {
            })
            .Step("Test Step 2", (context) =>
            {
            })
            .Step("Test Step 3", (context) =>
            {
            })
            .Step("Test Step 4", (context) =>
            {
            })
            .Step("Test Step 5", (context) =>
            {
            })
            .Run();
    }
}
