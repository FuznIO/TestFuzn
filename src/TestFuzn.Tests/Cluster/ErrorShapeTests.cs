using Fuzn.TestFuzn.Contracts.Results.Standard;
using Fuzn.TestFuzn.Internals.Results.Load;

namespace Fuzn.TestFuzn.Tests.Cluster;

[TestClass]
[Tags("unit")]
public class ErrorShapeTests : Test
{
    [Test]
    public async Task Verify_ids_and_long_numbers_are_replaced_and_status_codes_kept()
    {
        await Scenario()
            .Step("Ids, decimals and long numbers become placeholders", context =>
            {
                Assert.AreEqual("Order {n} not found", ErrorShape.Of("Order 4711 not found"));
                Assert.AreEqual("User {id} is locked", ErrorShape.Of("User 3f2504e0-4f89-11d3-9a0c-0305e82c3301 is locked"));
                Assert.AreEqual("Session {id} expired", ErrorShape.Of("Session 9f86d081884c expired"));
                Assert.AreEqual("Took {n} ms", ErrorShape.Of("Took 12.75 ms"));
            })
            .Step("Short numbers such as status codes stay", context =>
            {
                Assert.AreEqual("Expected 200 but was 500", ErrorShape.Of("Expected 200 but was 500"));
                Assert.AreEqual("Response status code does not indicate success: 404 (Not Found).",
                    ErrorShape.Of("Response status code does not indicate success: 404 (Not Found)."));
            });
    }

    [Test]
    public async Task Verify_errors_past_ten_kinds_are_counted_as_other_errors()
    {
        await Scenario()
            .Step("Messages that only differ by an id are one error; kinds past ten go to Other errors", context =>
            {
                var collector = new StepLoadCollector("Call", "call");
                var start = DateTime.UtcNow;
                for (var order = 1000; order < 1050; order++)
                    collector.Record(Failed($"Order {order} not found"), start, start);
                for (var kind = 0; kind < 15; kind++)
                    collector.Record(Failed($"Failure kind {(char)('A' + kind)}"), start, start);

                var result = collector.GetCurrentResult();
                var errors = result.Errors!.Values.ToList();
                Assert.AreEqual(50, errors.Single(e => e.Message == "Order {n} not found").Count);
                Assert.AreEqual(6, errors.Single(e => e.Message == ErrorShape.OtherErrors).Count);
                Assert.HasCount(11, errors);
                Assert.AreEqual(65, errors.Sum(e => e.Count));
            })
            .Step("A merged state keeps the other errors", context =>
            {
                var workerA = new StepLoadCollector("Call", "call");
                var workerB = new StepLoadCollector("Call", "call");
                var start = DateTime.UtcNow;
                for (var kind = 0; kind < 8; kind++)
                {
                    workerA.Record(Failed($"Worker A kind {(char)('A' + kind)}"), start, start);
                    workerB.Record(Failed($"Worker B kind {(char)('A' + kind)}"), start, start);
                }

                var merged = new StepLoadCollector("Call", "call");
                merged.Add(workerA.GetState());
                merged.Add(workerB.GetState());

                var errors = merged.GetCurrentResult().Errors!.Values.ToList();
                Assert.AreEqual(16, errors.Sum(e => e.Count));
                Assert.AreEqual(6, errors.Single(e => e.Message == ErrorShape.OtherErrors).Count);
            });
    }

    private static StepStandardResult Failed(string message) => new()
    {
        Name = "Call",
        Status = StepStatus.Failed,
        Duration = TimeSpan.FromMilliseconds(5),
        Exception = new InvalidOperationException(message)
    };
}
