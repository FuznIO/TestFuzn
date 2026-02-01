namespace Fuzn.TestFuzn.Tests.Attributes.Tags;

[TestClass]
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