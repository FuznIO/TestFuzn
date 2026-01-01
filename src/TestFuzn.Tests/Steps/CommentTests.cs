namespace Fuzn.TestFuzn.Tests.Steps;

[TestClass]
public class CommentTests : Test
{
    [Test]
    public async Task FeatureTest_Comment_Should_Be_Written_ToConsoleAndReports()
    {
        await Scenario()
            .Step("Step 1 - Write comment", (context) =>
            {
                context.Comment("This is a comment #1 for Step 1.");
                context.Comment("This is a comment #2 for Step 1.");
            })
            .Step("Step 2 - Write comment", (context) =>
            {
                context.Comment("This is a comment #1 for Step 2.");
                context.Comment("This is a comment #2 for Step 2.");
            })
            .Run();
    }

    [Test]
    public async Task LoadTest_Comment_Should_Be_Written_To_Log()
    {
        await Scenario()
            .Step("Step 1 - Write comment", (context) =>
            {
                context.Comment("This is a comment #1 for Step 1.");
                context.Comment("This is a comment #2 for Step 1.");
            })
            .Step("Step 2 - Write comment", (context) =>
            {
                context.Comment("This is a comment #1 for Step 2.");
                context.Comment("This is a comment #2 for Step 2.");
            })
            .Load().Simulations((context, simulations) => simulations.OneTimeLoad(5))
            .Run();
    }
}
