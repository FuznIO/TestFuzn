namespace Fuzn.TestFuzn.Tests.Attributes;

[TestClass]
public class SkipAttributeTests : Test
{
    [Test]
    [Skip("This test is skipped for demonstration purposes.")]
    public async Task TestHasSkippedAttributeWithReasonAndShouldNotBeExecuted()
    {
        await Scenario()
            .Step("Step1", context =>
            {
                throw new Exception("This step should not be executed.");
            })
            .Run();
    }

    [Test]
    [Skip()]
    public async Task TestHasSkippedAttributeWithoutReasonAndShouldNotBeExecuted()
    {
        await Scenario()
            .Step("Step1", context =>
            {
                throw new Exception("This step should not be executed.");
            })
            .Run();
    }
}
