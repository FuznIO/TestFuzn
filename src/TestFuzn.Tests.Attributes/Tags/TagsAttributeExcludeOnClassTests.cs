namespace Fuzn.TestFuzn.Tests.Attributes.Tags;

[TestClass]
[Tags("TagInclude1", "TagExclude1")]
[TargetEnvironments("test")]
public class TagsAttributeExcludeOnClassTests : Test
{
    [Test]
    public async Task TestShouldNotRun()
    {
        Assert.Fail("This test should have been excluded by TagExclude1 attribute.");
    }
}