using Fuzn.TestFuzn.Internals;
using Fuzn.TestFuzn.Internals.Cluster;
using Fuzn.TestFuzn.Internals.InputData;
using Fuzn.TestFuzn.Internals.State;

namespace Fuzn.TestFuzn.Tests.Cluster;

[TestClass]
[Tags("unit")]
public class InputDataSliceTests : Test
{
    [Test]
    public async Task Verify_each_worker_loops_over_its_own_slice()
    {
        await Scenario()
            .Step("Worker i owns the items at i, i+N, i+2N", context =>
            {
                var items = Enumerable.Range(0, 10).Cast<object>().ToList();
                CollectionAssert.AreEqual(new object[] { 1, 4, 7 }, InputDataFeeder.Slice(items, 1, 3));
                CollectionAssert.AreEqual(new object[] { 0, 3, 6, 9 }, InputDataFeeder.Slice(items, 0, 3));
            })
            .Step("Loop on a worker cycles through its slice only", context =>
            {
                var feeder = Feeder(Enumerable.Range(0, 6).Cast<object>().ToList(), InputDataBehavior.Loop, workerIndex: 1, workerCount: 2);
                CollectionAssert.AreEqual(new object[] { 1, 3, 5, 1, 3 }, Take(feeder, 5));
            })
            .Step("LoopThenRepeatLast loops the slice once, then repeats the last item of the full list", context =>
            {
                var feeder = Feeder(Enumerable.Range(0, 6).Cast<object>().ToList(), InputDataBehavior.LoopThenRepeatLast, workerIndex: 0, workerCount: 2);
                CollectionAssert.AreEqual(new object[] { 0, 2, 4, 5, 5 }, Take(feeder, 5));
            })
            .Step("A single node keeps today's behavior", context =>
            {
                var feeder = Feeder(Enumerable.Range(0, 3).Cast<object>().ToList(), InputDataBehavior.LoopThenRepeatLast, workerIndex: 0, workerCount: 1);
                CollectionAssert.AreEqual(new object[] { 0, 1, 2, 2 }, Take(feeder, 4));
            })
            .Step("A worker with an empty slice fails with a clear message when it must loop", context =>
            {
                var feeder = Feeder(new List<object> { "only" }, InputDataBehavior.Loop, workerIndex: 1, workerCount: 2);
                var exception = Assert.ThrowsExactly<InvalidOperationException>(() => feeder.GetNextInput("S"));
                Assert.Contains("1 items for 2 workers", exception.Message);
            });
    }

    private static List<object> Take(InputDataFeeder feeder, int count)
    {
        return Enumerable.Range(0, count).Select(_ => feeder.GetNextInput("S")).ToList();
    }

    private static InputDataFeeder Feeder(List<object> items, InputDataBehavior behavior, int workerIndex, int workerCount)
    {
        var session = new TestSession("slice-" + Guid.NewGuid().ToString("N"));
        session.Cluster = new ClusterSessionInfo { WorkerIndex = workerIndex, WorkerCount = workerCount };

        var scenario = new Scenario("S");
        scenario.InputDataInfo.AddParams(items.ToArray());
        scenario.InputDataInfo.InputDataBehavior = behavior;

        var state = new TestExecutionState(session);
        state.Scenarios.Add(scenario);

        var feeder = new InputDataFeeder();
        feeder.Init(state);
        return feeder;
    }
}
