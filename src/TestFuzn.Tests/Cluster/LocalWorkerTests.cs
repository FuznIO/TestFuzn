using Fuzn.TestFuzn.Contracts.Results.Load;
using Fuzn.TestFuzn.Internals;
using Fuzn.TestFuzn.Internals.Cluster;
using Fuzn.TestFuzn.Internals.State;

namespace Fuzn.TestFuzn.Tests.Cluster;

[TestClass]
[Tags("unit")]
public class LocalWorkerTests : Test
{
    [Test]
    public async Task Verify_a_run_without_a_cluster_reports_its_own_machine()
    {
        await Scenario()
            .Step("The machine has its real name, OS, cores and the phase of the scenario", context =>
            {
                var state = new TestExecutionState(new TestSession("local-" + Guid.NewGuid().ToString("N")));
                var worker = new LocalWorker();

                var warmup = worker.Sample(state, new ScenarioLoadResult { WarmupStartTime = DateTime.UtcNow });
                var done = worker.Sample(state, new ScenarioLoadResult { MeasurementStartTime = DateTime.UtcNow, IsCompleted = true });

                Assert.HasCount(1, done);
                Assert.AreEqual(Environment.MachineName, done[0].Id);
                Assert.IsFalse(string.IsNullOrEmpty(done[0].Os));
                Assert.AreEqual(Environment.ProcessorCount, done[0].CpuCores);
                Assert.AreEqual(ScenarioPhase.Warmup, warmup[0].State);
                Assert.AreEqual(ScenarioPhase.Completed, done[0].State);
            })
            .Step("Max values never drop below the average or the latest sample", context =>
            {
                var state = new TestExecutionState(new TestSession("local-" + Guid.NewGuid().ToString("N")));
                var worker = new LocalWorker();

                worker.Sample(state, new ScenarioLoadResult());
                var latest = worker.Sample(state, new ScenarioLoadResult())[0];

                Assert.IsGreaterThanOrEqualTo(latest.CpuAveragePercent, latest.CpuMaxPercent);
                Assert.IsGreaterThanOrEqualTo(latest.CpuPercent, latest.CpuMaxPercent);
                Assert.IsGreaterThanOrEqualTo(latest.MemoryMb, latest.MemoryMaxMb);
                Assert.IsGreaterThan(0, latest.MemoryMaxMb);
            });
    }
}
