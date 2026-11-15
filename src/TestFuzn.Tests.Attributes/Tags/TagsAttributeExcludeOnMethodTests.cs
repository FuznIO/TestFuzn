namespace Fuzn.TestFuzn.Tests.Attributes.Tags;

[TestClass]
[Tags("integration")]
[TargetEnvironments("test")]
public class TagsAttributeExcludeOnMethodTests : Test
{
    [Test]
    [Tags("TagInclude1", "TagExclude1")]
    public async Task TestShouldNotRun()
    {
        Assert.Fail();
    }
}