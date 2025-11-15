namespace Fuzn.TestFuzn.Tests.Steps;

[FeatureTest]
public class AttachmentTests : BaseFeatureTest
{
    [ScenarioTest]
    public async Task Verify_attachments()
    {
        await Scenario()
            .InputData("user1", "user2")
            .Step("Step 1 with no attachment", (context) => { })
            .Step("Step 2 with single attachment", async context =>
            {
                await context.Attach($"TestAttachment1_{context.InputData<string>()}.txt", "This is a test attachment 1 content.");
            })
            .Step("Step 3 with multiple attachment", async context =>
            {
                await context.Attach($"TestAttachment2_{context.InputData<string>()}.txt", "This is a test attachment 2 content.");
                await context.Attach($"TestAttachment3_{context.InputData<string>()}.txt", "This is a test attachment 3 content.");
            })
            .Run();
    }
}