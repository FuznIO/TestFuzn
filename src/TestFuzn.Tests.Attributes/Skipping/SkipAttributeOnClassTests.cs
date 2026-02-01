namespace Fuzn.TestFuzn.Tests.Attributes.Skipping;

[TestClass]
[Skip]
[Tags("TagInclude1")]
[TargetEnvironments("test1")]
public class SkipAttributeOnClassTests : Test
{
    [Test]    
    public async Task TestShouldBeSkipped()
    {
        Assert.Fail("This test should have been skipped and not executed.");
    }
}