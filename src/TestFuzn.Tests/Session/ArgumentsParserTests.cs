using Fuzn.TestFuzn.Internals;

namespace Fuzn.TestFuzn.Tests.Session;

[TestClass]
public class ArgumentsParserTests : Test
{
    private static ArgumentsParser CreateParser()
    {
        return new ArgumentsParser(new EnvironmentWrapper());
    }

    [Test]
    public async Task Verify_bare_flags_parse_beside_key_value_arguments()
    {
        await Scenario()
            .Step("A bare flag is recorded as set; single-dash arguments are not recorded", context =>
            {
                var parsed = CreateParser().Parse(new[] { "--test-name", "-v" });

                Assert.HasCount(1, parsed);
                Assert.AreEqual(ArgumentsParser.FlagValue, parsed["test-name"]);
                Assert.AreEqual(ArgumentsParser.FlagValue, parsed["TEST-NAME"]);
                Assert.IsFalse(parsed.ContainsKey("v"));
            })
            .Step("Key/value arguments parse as before: the value trimmed, then stripped of surrounding quotes (what the quotes held is kept as is), the key case-insensitive", context =>
            {
                var parsed = CreateParser().Parse(new[] { "--test-name= My.Tests.Load_products ", "--tags-filter-include=' smoke, load '", "--results-directory=\"/tmp/results\"" });

                Assert.HasCount(3, parsed);
                Assert.AreEqual("My.Tests.Load_products", parsed["test-name"]);
                Assert.AreEqual("My.Tests.Load_products", parsed["TEST-NAME"]);
                Assert.AreEqual(" smoke, load ", parsed["tags-filter-include"]);
                Assert.AreEqual("/tmp/results", parsed["results-directory"]);
            })
            .Step("A bare flag and a key/value argument parse in either order", context =>
            {
                var flagFirst = CreateParser().Parse(new[] { "--verbose", "--test-name=My.Tests.Load_products" });
                var flagLast = CreateParser().Parse(new[] { "--test-name=My.Tests.Load_products", "--verbose" });

                foreach (var parsed in new[] { flagFirst, flagLast })
                {
                    Assert.HasCount(2, parsed);
                    Assert.AreEqual(ArgumentsParser.FlagValue, parsed["verbose"]);
                    Assert.AreEqual("My.Tests.Load_products", parsed["test-name"]);
                }
            })
            .Step("A repeated argument keeps its last value, for flags and key/value arguments alike", context =>
            {
                Assert.AreEqual("false", CreateParser().Parse(new[] { "--verbose", "--verbose=false" })["verbose"]);
                Assert.AreEqual(ArgumentsParser.FlagValue, CreateParser().Parse(new[] { "--verbose=false", "--verbose" })["verbose"]);
                Assert.AreEqual("B", CreateParser().Parse(new[] { "--test-name=A", "--test-name=B" })["test-name"]);
            })
            .Step("An unknown argument is recorded like any other and changes nothing for the arguments that are read", context =>
            {
                var parsed = CreateParser().Parse(new[] { "--no-such-flag", "--test-name=My.Tests.Load_products" });

                Assert.HasCount(2, parsed);
                Assert.AreEqual(ArgumentsParser.FlagValue, parsed["no-such-flag"]);
                Assert.AreEqual("My.Tests.Load_products", CreateParser().GetValueFromArgsOrEnvironmentVariable(parsed, "test-name", "TESTFUZN_TEST_NAME"));
            })
            .Step("No arguments, null arguments and a lone -- parse to nothing", context =>
            {
                Assert.IsEmpty(CreateParser().Parse(Array.Empty<string>()));
                Assert.IsEmpty(CreateParser().Parse(null!));
                Assert.IsEmpty(CreateParser().Parse(new[] { "--" }));
            })
            .Run();
    }
}
