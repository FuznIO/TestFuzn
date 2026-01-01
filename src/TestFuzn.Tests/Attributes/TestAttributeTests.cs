namespace Fuzn.TestFuzn.Tests.Attributes;

[TestClass]
public class TestAttributeTests : Test
{
    [Test]
    public async Task WhenTestAttributeIsUsedScenarioasShouldBeExecuted()
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
