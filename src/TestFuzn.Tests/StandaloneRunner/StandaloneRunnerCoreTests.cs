using Fuzn.TestFuzn.StandaloneRunner;
using Fuzn.TestFuzn.Tests.Terminal;

namespace Fuzn.TestFuzn.Tests.StandaloneRunner;

/// <summary>
/// Pins the runner core's handling of a <c>--test-name</c> given without a value, which the
/// argument parser records as a bare flag: an invocation error with the usage message, written
/// through the framework adapter, exit code 1 — never a lookup of a test named "true", never a
/// silent fall-through to the selection menu (which would exit 0 on the redirected input here).
/// </summary>
[TestClass]
public class StandaloneRunnerCoreTests : Test
{
    private static readonly string UsageEvent = FakeTestFrameworkAdapter.MarkupEventPrefix + "[red]" + StandaloneRunnerCore.TestNameUsage + "[/]";

    [Test]
    public async Task Verify_a_test_name_without_a_value_is_an_invocation_error()
    {
        await Scenario()
            .Step("A bare --test-name exits 1 with the usage message written through the adapter, and nothing else", async context =>
            {
                var events = new List<string>();
                var exitCode = await new StandaloneRunnerCore().Run<FakeStartup>(typeof(StandaloneRunnerCoreTests).Assembly, new[] { "run", "--test-name" }, () => new FakeTestFrameworkAdapter(events));

                Assert.AreEqual(1, exitCode);
                var usage = Assert.ContainsSingle(events);
                Assert.AreEqual(UsageEvent, usage);
            })
            .Step("--test-name with a space instead of = is the same error, whatever follows", async context =>
            {
                var events = new List<string>();
                var exitCode = await new StandaloneRunnerCore().Run<FakeStartup>(typeof(StandaloneRunnerCoreTests).Assembly, new[] { "run", "--test-name", "Fuzn.TestFuzn.Tests.SomeTests.Some_test" }, () => new FakeTestFrameworkAdapter(events));

                Assert.AreEqual(1, exitCode);
                var usage = Assert.ContainsSingle(events);
                Assert.AreEqual(UsageEvent, usage);
            })
            .Step("A test name that names no test is still reported as not found with exit 1, without an adapter", async context =>
            {
                var events = new List<string>();
                var exitCode = await new StandaloneRunnerCore().Run<FakeStartup>(typeof(StandaloneRunnerCoreTests).Assembly, new[] { "run", "--test-name=Fuzn.TestFuzn.Tests.SomeTests.Does_not_exist" }, () => new FakeTestFrameworkAdapter(events));

                Assert.AreEqual(1, exitCode);
                Assert.IsEmpty(events);
            })
            .Run();
    }
}
