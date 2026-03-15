using Microsoft.EntityFrameworkCore;

namespace Fuzn.TestFuzn.Tests.Attributes.GlobalStates;

[TestClass]
public class GlobalStateTests
{
    [TestMethod]
    public void TargetEnvironmentReturnsExpectedValue()
    {
        Assert.AreEqual("test", GlobalState.TargetEnvironment);
    }

    [TestMethod]
    public void ExecutionEnvironmentReturnsExpectedValue()
    {
        Assert.AreEqual("dev", GlobalState.ExecutionEnvironment);
    }

    [TestMethod]
    public void AppConfigurationReturnsNonNullValue()
    {
        Assert.IsNotNull(GlobalState.AppConfiguration);
        Assert.AreEqual("root", GlobalState.AppConfiguration.GetRequiredValue<string>("ValueNeverOverride"));
        Assert.AreEqual("dev", GlobalState.AppConfiguration.GetRequiredValue<string>("ValueExecOverride"));
        Assert.AreEqual("test", GlobalState.AppConfiguration.GetRequiredValue<string>("ValueTargetOverride"));
    }

    [TestMethod]
    public void NodeNameReturnsCurrentMachineName()
    {
        Assert.AreEqual(Environment.MachineName, GlobalState.NodeName);
    }

    [TestMethod]
    public void ServiceProviderReturnsNonNullValue()
    {
        Assert.IsNotNull(GlobalState.ServiceProvider);
    }

    [TestMethod]
    public void LoggerReturnsNonNullValue()
    {
        Assert.IsNotNull(GlobalState.Logger);
    }

    [TestMethod]
    public void TestsOutputDirectoryContainsTestRunId()
    {
        Assert.IsTrue(GlobalState.TestsOutputDirectory.Contains(GlobalState.TestRunId));
    }

    [TestMethod]
    public void TestRunIdReturnsNonEmptyValue()
    {
        Assert.IsFalse(string.IsNullOrEmpty(GlobalState.TestRunId));
    }
}
