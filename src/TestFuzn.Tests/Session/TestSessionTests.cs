using Fuzn.TestFuzn.Internals;
using Fuzn.TestFuzn.Internals.FileConfiguration;

namespace Fuzn.TestFuzn.Tests.Session;

[TestClass]
public class TestSessionTests : Test, IStartup
{
    [ClassInitialize]
    public static async Task ClassInitialize(TestContext testContext)
    {
        var testSession = new TestSession(nameof(TestSessionTests));
        var fileSystem = new FileSystem();
        await testSession.Init<TestSessionTests>(
            new EnvironmentWrapper(),
            fileSystem  ,
            new ConfigurationLoader(),
            new ArgumentsParser(new EnvironmentWrapper()),
            new MsTestRunnerAdapter(testContext));
        testSession.TestRunId = "TestSessionOverride";
        TestSessionRegistry.Add(testSession);
    }

    public void Configure(TestFuznConfiguration configuration)
    {
    }

    [Test(TestSessionId = nameof(TestSessionTests))]
    public async Task EnsureTestSessionOverrideWorks()
    {
        Assert.AreEqual("TestSessionOverride", TestSession.Current.TestRunId);

        await Scenario()
                .Step("Step 1", (context) =>
                {
                    Assert.AreEqual("TestSessionOverride", context.Info.TestRunId);
                })
                .Run();
    }
}