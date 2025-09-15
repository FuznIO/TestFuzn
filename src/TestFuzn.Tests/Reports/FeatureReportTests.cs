namespace Fuzn.TestFuzn.Tests.Reports;

[FeatureTest]
public class FeatureReportTests : BaseFeatureTest
{
    [ScenarioTest]
    public async Task Feature1()
    {
        await Scenario()
            .Id("ID-1234")
            .InputData("user1", "user2", "user")
            .Metadata("ScenarioKey1", "Value1")
            .Metadata("ScenarioKey2", "Value2")
            .Step("Test Step 1", (context) =>
            {
                context.CurrentStep.Metadata("Step1Key1", "Value1");
                context.CurrentStep.Metadata("Step1Key2", "Value2");
                context.CurrentStep.Comment("Comment 1 Sub step 1");
                context.CurrentStep.Comment("Comment 2 Sub step 1");
                context.Step("Sub Step 1", (subContext) =>
                {
                    context.CurrentStep.Metadata("SubStep1", "Value1");
                    context.CurrentStep.Metadata("SubStep2", "Value2");
                    context.CurrentStep.Comment("Comment 1 Sub step 1");
                    context.CurrentStep.Comment("Comment 2 Sub step 1");

                    context.Step("Sub Sub Step 1", (subSubContext) =>
                    {
                        context.CurrentStep.Metadata("SubSubStep1", "Value1");
                        context.CurrentStep.Metadata("SubSubStep2", "Value2");
                        context.CurrentStep.Comment("Comment 1 Sub Sub step 1");
                        context.CurrentStep.Comment("Comment 2 Sub Sub step 1");
                    });
                });
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
