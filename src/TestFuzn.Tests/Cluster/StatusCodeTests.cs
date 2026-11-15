using Fuzn.TestFuzn.Contracts.Results.Standard;
using Fuzn.TestFuzn.Internals.Results.Load;

namespace Fuzn.TestFuzn.Tests.Cluster;

[TestClass]
[Tags("unit")]
public class StatusCodeTests : Test
{
    [Test]
    public async Task Verify_status_codes_add_up_per_step_and_across_workers()
    {
        await Scenario()
            .Step("Each iteration's codes add up, and a merged state keeps them", context =>
            {
                var workerA = new StepLoadCollector("Call", "call");
                var workerB = new StepLoadCollector("Call", "call");
                var start = DateTime.UtcNow;
                workerA.Record(Result(new() { [200] = 3, [503] = 1 }), start, start);
                workerA.Record(Result(new() { [200] = 2 }), start, start);
                workerB.Record(Result(new() { [200] = 4, [429] = 2 }), start, start);

                var single = workerA.GetCurrentResult().StatusCodes!;
                Assert.AreEqual(5, single[200]);
                Assert.AreEqual(1, single[503]);

                var merged = new StepLoadCollector("Call", "call");
                merged.Add(workerA.GetState());
                merged.Add(workerB.GetState());
                var codes = merged.GetCurrentResult().StatusCodes!;
                Assert.AreEqual(9, codes[200]);
                Assert.AreEqual(2, codes[429]);
                Assert.AreEqual(1, codes[503]);
            });
    }

    private static StepStandardResult Result(Dictionary<int, int> codes) => new()
    {
        Name = "Call",
        Status = StepStatus.Passed,
        Duration = TimeSpan.FromMilliseconds(5),
        StatusCodes = codes
    };
}
