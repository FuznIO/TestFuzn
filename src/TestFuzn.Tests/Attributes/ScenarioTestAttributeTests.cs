namespace Fuzn.TestFuzn.Tests.Attributes;

[TestClass]
public class TestAttributeTests : TestBase
{
    [Test]
    public async Task TestAttributeUsedShouldExecuteScenario()
    {
        var wasRun = false;

        await Scenario()
            .Step("Step1", context =>
            {
                wasRun = true;
            })
            .Run();

        Assert.IsTrue(wasRun);
    }
}
