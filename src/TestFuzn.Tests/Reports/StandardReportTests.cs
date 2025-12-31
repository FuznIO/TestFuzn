namespace Fuzn.TestFuzn.Tests.Reports;

[TestClass]
public class StandardReportTests : TestBase
{
    [Test]
    public async Task WithSteps()
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

    [Test]
    [Tags("Tag1", "Tag2", "Tag2")]
    public async Task WithSubStepsAndMetadata()
    {
        await Scenario()
            .Id("ID-1234")
            .InputData("user1", "user2", "user")
            .Metadata("ScenarioKey1", "Value1")
            .Metadata("ScenarioKey2", "Value2")
            .Step("Test Step 1", (context) =>
            {
                context.Comment("Comment 1 Sub step 1");
                context.Comment("Comment 2 Sub step 1");
                context.Step("Sub Step 1", (subContext) =>
                {
                    subContext.Comment("Comment 1 Sub step 1");
                    subContext.Comment("Comment 2 Sub step 1");

                    subContext.Step("Sub Sub Step 1", (subSubContext) =>
                    {
                        subSubContext.Comment("Comment 1 Sub Sub step 1");
                        subSubContext.Comment("Comment 2 Sub Sub step 1");

                        subSubContext.Step("Sub Sub Sub Step 1", (subSubSubContext) =>
                        {
                            subSubSubContext.Comment("Comment 1 Sub Sub Sub step 1");
                            subSubSubContext.Comment("Comment 2 Sub Sub Sub step 1");

                            subSubSubContext.Step("Sub Sub Sub Sub Step 1", (subSubSubSubContext) =>
                            {
                                subSubSubSubContext.Comment("Comment 1 Sub Sub Sub Sub step 1");
                                subSubSubSubContext.Comment("Comment 2 Sub Sub Sub Sub step 1");
                            });
                        });
                    });
                });
            })
            .Step("Test Step 2", (context) =>
            {
            })
            .Step("Test Step 3", "ID-Step3-123", (context) =>
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
