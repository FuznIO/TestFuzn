using Fuzn.TestFuzn.Internals.Cockpit;

namespace Fuzn.TestFuzn.Tests.Cockpit;

[TestClass]
[Tags("unit")]
public class CockpitSettingsTests : Test
{
    [Test]
    public async Task Verify_the_source_comes_from_the_cockpit_source_argument()
    {
        await Scenario()
            .Step("--cockpit-source=<name> sets the source, whatever the variable holds", context =>
            {
                var args = new Dictionary<string, string> { [CockpitSettings.SourceArgument] = "soak" };

                Assert.AreEqual("soak", CockpitSettings.Load(null, args).Source);
            })
            .Step("The argument name is case-blind, as the parser's dictionary is", context =>
            {
                var args = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["COCKPIT-SOURCE"] = "soak" };

                Assert.AreEqual("soak", CockpitSettings.Load(null, args).Source);
            })
            .Step("A bare --cockpit-source is an error that shows how to pass the name", context =>
            {
                var args = new Dictionary<string, string> { [CockpitSettings.SourceArgument] = Internals.ArgumentsParser.FlagValue };

                var error = Assert.ThrowsExactly<InvalidOperationException>(() => CockpitSettings.Load(null, args));
                Assert.AreEqual(CockpitSettings.SourceUsage, error.Message);
            })
            .Step("Without the argument, the source is the variable's, as before", context =>
            {
                var expected = Environment.GetEnvironmentVariable(CockpitSettings.SourceVariable);
                if (string.IsNullOrWhiteSpace(expected))
                    expected = null;

                Assert.AreEqual(expected, CockpitSettings.Load(null, new Dictionary<string, string>()).Source);
                Assert.AreEqual(expected, CockpitSettings.Load(null).Source);
            });
    }
}
