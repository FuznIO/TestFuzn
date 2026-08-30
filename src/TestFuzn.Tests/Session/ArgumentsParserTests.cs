using Fuzn.TestFuzn.Internals;
using Fuzn.TestFuzn.StandaloneRunner;

namespace Fuzn.TestFuzn.Tests.Session;

/// <summary>
/// Pins <see cref="ArgumentsParser"/>: the <c>--key=value</c> form as it always parsed, the bare
/// boolean flag form (<c>--demo</c>) beside it in either order, the documented behaviour
/// for an unknown flag (recorded, harmless), a repeated one (last wins) and an explicit value
/// (<c>--demo=false</c> is not set), and the whole-seconds duration reader behind
/// <c>--demo-duration</c>: the default for an absent argument, the duration for a readable one,
/// and false — the caller's invocation error — for anything else.
/// </summary>
[TestClass]
public class ArgumentsParserTests : Test
{
    private const string DemoFlag = StandaloneRunnerCore.DemoFlag;
    private const string DemoDurationArgument = StandaloneRunnerCore.DemoDurationArgument;

    private static readonly TimeSpan MinimumDuration = TimeSpan.FromSeconds(20);
    private static readonly TimeSpan DefaultDuration = TimeSpan.FromSeconds(60);

    private static ArgumentsParser CreateParser()
    {
        return new ArgumentsParser(new EnvironmentWrapper());
    }

    [Test]
    public async Task Verify_bare_flags_parse_beside_key_value_arguments()
    {
        await Scenario()
            .Step("A bare flag is recorded as set; the verb and single-dash arguments are not recorded", context =>
            {
                var parsed = CreateParser().Parse(new[] { "run", "--demo", "-v" });

                Assert.HasCount(1, parsed);
                Assert.AreEqual(ArgumentsParser.FlagValue, parsed[DemoFlag]);
                Assert.IsTrue(ArgumentsParser.HasFlag(parsed, DemoFlag));
                Assert.IsTrue(ArgumentsParser.HasFlag(parsed, "DEMO"));
                Assert.IsFalse(parsed.ContainsKey("run"));
                Assert.IsFalse(parsed.ContainsKey("v"));
            })
            .Step("Key/value arguments parse as before: the value trimmed, then stripped of surrounding quotes (what the quotes held is kept as is), the key case-insensitive", context =>
            {
                var parsed = CreateParser().Parse(new[] { "run", "--test-name= My.Tests.Load_products ", "--tags-filter-include=' smoke, load '", "--results-directory=\"/tmp/results\"" });

                Assert.HasCount(3, parsed);
                Assert.AreEqual("My.Tests.Load_products", parsed["test-name"]);
                Assert.AreEqual("My.Tests.Load_products", parsed["TEST-NAME"]);
                Assert.AreEqual(" smoke, load ", parsed["tags-filter-include"]);
                Assert.AreEqual("/tmp/results", parsed["results-directory"]);
                Assert.IsFalse(ArgumentsParser.HasFlag(parsed, DemoFlag));
            })
            .Step("A bare flag and a key/value argument parse in either order", context =>
            {
                var flagFirst = CreateParser().Parse(new[] { "run", "--demo", "--test-name=My.Tests.Load_products" });
                var flagLast = CreateParser().Parse(new[] { "run", "--test-name=My.Tests.Load_products", "--demo" });

                foreach (var parsed in new[] { flagFirst, flagLast })
                {
                    Assert.HasCount(2, parsed);
                    Assert.IsTrue(ArgumentsParser.HasFlag(parsed, DemoFlag));
                    Assert.AreEqual("My.Tests.Load_products", parsed["test-name"]);
                }
            })
            .Step("No arguments, null arguments and a lone -- parse to nothing", context =>
            {
                Assert.IsEmpty(CreateParser().Parse(Array.Empty<string>()));
                Assert.IsEmpty(CreateParser().Parse(null!));
                Assert.IsEmpty(CreateParser().Parse(new[] { "run", "--" }));
                Assert.IsFalse(ArgumentsParser.HasFlag(null, DemoFlag));
            })
            .Run();
    }

    [Test]
    public async Task Verify_flag_values_and_repeated_or_unknown_flags()
    {
        await Scenario()
            .Step("An explicit value decides a flag: false and 0 (any case) are not set, anything else is", context =>
            {
                foreach (var argument in new[] { "--demo=false", "--demo=FALSE", "--demo=0", "--demo= false " })
                {
                    var parsed = CreateParser().Parse(new[] { "run", argument });
                    Assert.IsTrue(parsed.ContainsKey(DemoFlag), argument);
                    Assert.IsFalse(ArgumentsParser.HasFlag(parsed, DemoFlag), argument);
                }

                foreach (var argument in new[] { "--demo=true", "--demo=yes", "--demo=1", "--demo=" })
                {
                    var parsed = CreateParser().Parse(new[] { "run", argument });
                    Assert.IsTrue(ArgumentsParser.HasFlag(parsed, DemoFlag), argument);
                }
            })
            .Step("A repeated argument keeps its last value, for flags and key/value arguments alike", context =>
            {
                Assert.IsFalse(ArgumentsParser.HasFlag(CreateParser().Parse(new[] { "run", "--demo", "--demo=false" }), DemoFlag));
                Assert.IsTrue(ArgumentsParser.HasFlag(CreateParser().Parse(new[] { "run", "--demo=false", "--demo" }), DemoFlag));
                Assert.AreEqual("B", CreateParser().Parse(new[] { "--test-name=A", "--test-name=B" })["test-name"]);
            })
            .Step("An unknown flag is recorded like any other and changes nothing for the arguments that are read", context =>
            {
                var parsed = CreateParser().Parse(new[] { "run", "--no-such-flag", "--test-name=My.Tests.Load_products" });

                Assert.HasCount(2, parsed);
                Assert.IsTrue(ArgumentsParser.HasFlag(parsed, "no-such-flag"));
                Assert.IsFalse(ArgumentsParser.HasFlag(parsed, DemoFlag));
                Assert.AreEqual("My.Tests.Load_products", CreateParser().GetValueFromArgsOrEnvironmentVariable(parsed, "test-name", "TESTFUZN_TEST_NAME"));
            })
            .Run();
    }

    [Test]
    public async Task Verify_a_duration_argument_reads_whole_seconds_and_reports_an_unreadable_one()
    {
        await Scenario()
            .Step("An absent argument is no error: the default stands. A whole number of seconds at or above the minimum is the duration, whatever spacing or quotes it came in", context =>
            {
                Assert.IsTrue(ArgumentsParser.TryGetDuration(CreateParser().Parse(new[] { "run", "--demo" }), DemoDurationArgument, MinimumDuration, DefaultDuration, out var absent));
                Assert.AreEqual(DefaultDuration, absent);
                Assert.IsTrue(ArgumentsParser.TryGetDuration(null, DemoDurationArgument, MinimumDuration, DefaultDuration, out var noArguments));
                Assert.AreEqual(DefaultDuration, noArguments);

                foreach (var seconds in new[] { 20, 21, 45, 3600 })
                {
                    var parsed = CreateParser().Parse(new[] { "run", "--demo-duration=" + seconds });
                    Assert.IsTrue(ArgumentsParser.TryGetDuration(parsed, DemoDurationArgument, MinimumDuration, DefaultDuration, out var duration), seconds.ToString());
                    Assert.AreEqual(TimeSpan.FromSeconds(seconds), duration, seconds.ToString());
                }

                var quoted = CreateParser().Parse(new[] { "run", "--demo-duration=' 30 '" });
                Assert.IsTrue(ArgumentsParser.TryGetDuration(quoted, DemoDurationArgument, MinimumDuration, DefaultDuration, out var spaced));
                Assert.AreEqual(TimeSpan.FromSeconds(30), spaced);
            })
            .Step("A value below the minimum, a fraction, text, an empty value, a bare flag or a number too large is unreadable: false, with the default left in place for the caller to report the error over", context =>
            {
                foreach (var argument in new[] { "--demo-duration=19", "--demo-duration=0", "--demo-duration=-1", "--demo-duration=30.5", "--demo-duration=30s", "--demo-duration=", "--demo-duration", "--demo-duration=99999999999" })
                {
                    var parsed = CreateParser().Parse(new[] { "run", argument });

                    Assert.IsFalse(ArgumentsParser.TryGetDuration(parsed, DemoDurationArgument, MinimumDuration, DefaultDuration, out var duration), argument);
                    Assert.AreEqual(DefaultDuration, duration, argument);
                }
            })
            .Run();
    }

}
