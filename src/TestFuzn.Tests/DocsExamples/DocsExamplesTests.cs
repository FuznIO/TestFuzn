using System;
using System.Collections.Generic;
using System.Text;

namespace Fuzn.TestFuzn.Tests.DocsExamples;


[TestClass]
public class DocsExamplesTests : Test
{
    [Test]
    public async Task MultipleScenarios()
    {
        var scenario2 = Scenario("Second scenario")
            .Step("Step 1", (context) =>
            {
            })
            .Load().Simulations((context, simulations) => simulations.OneTimeLoad(30));

        await Scenario("First scenario")
            .Id("Scenario-1")
            .Step("Step 1", (context) =>
            {
            })
            .Step("Step 2", async (context) =>
            {
            })
            .Load().Simulations((context, simulations) => simulations.OneTimeLoad(20))
            .Load().IncludeScenario(scenario2)
            .Run();
    }
}

