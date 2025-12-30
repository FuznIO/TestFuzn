namespace Fuzn.TestFuzn.Tests.ScenarioTestAttr;

[TestClass]
public class ScenarioTestAttributeTests : TestBase
{
    [TestMethod]
    public async Task ShouldFail_MS_Test_Method_Is_Not_Supported()
    {
        var catchWasRun = false;

        try
        {
            var stepWasRun = false;

            await Scenario()
                .Step("Step1", context =>
                {
                    stepWasRun = true;
                })
                .Run();

            Assert.IsFalse(stepWasRun);
        }
        catch (Exception ex)
        {
            Assert.Contains("uses [TestMethod]. Use [ScenarioTest] instead for scenario-based tests.", ex.Message);
            catchWasRun = true;
        }
        finally
        {
            Assert.IsTrue(catchWasRun);
        }
    }

    [Test]
    public async Task ScenarioTest_DefaultConstructor_Should_Execute_Scenario()
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

    [Test]
    [Skip("Skipping this test for demonstration purposes.")]
    public async Task ScenarioTest_Skip_ShouldOnlyAddToReportNotExecute()
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
