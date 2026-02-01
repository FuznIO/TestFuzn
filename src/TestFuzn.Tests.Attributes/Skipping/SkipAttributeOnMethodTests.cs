namespace Fuzn.TestFuzn.Tests.Attributes.Skipping;

[TestClass]
[TargetEnvironments("test")]
public class SkipAttributeOnMethodTests : Test
{
    [Test]
    [Skip]
    [Tags("TagInclude1")]
    public async Task TestShouldBeSkipped()
    {
        Assert.Fail("This test should have been skipped and not executed.");
    }

    [Test]
    [Skip("Test is skipped due to...")]
    [Tags("TagInclude1")]
    public async Task TestShouldBeSkippedWithReason()
    {
        Assert.Fail("This test should have been skipped and not executed.");
    }
}
