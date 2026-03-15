using Fuzn.TestFuzn.Internals;

namespace Fuzn.TestFuzn.Tests.Attributes.Tags;

[TestClass]
[Tags("TagInclude1")]
[TargetEnvironments("test")]
public class TagsAttributeIncludeOnClassTests : Test
{
    [Test]
    public async Task TestShouldRun()
    {
        await Scenario()
            .Step("Step 1", async (context) =>
            {
                Assert.Contains("TagInclude1", TestSession.Current.Configuration.TagsFilterInclude);
                await Task.CompletedTask;
            })
            .Run();
    }
}