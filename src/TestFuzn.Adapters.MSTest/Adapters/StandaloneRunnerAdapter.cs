using Fuzn.TestFuzn.StandaloneRunner;
using System.Reflection;

namespace Fuzn.TestFuzn.Adapters;

internal class StandaloneRunnerAdapter : BaseStandaloneRunnerAdapter
{
    public override async Task ExecuteTestMethod(ITest test, MethodInfo methodInfo)
    {
        var testContextProperty = test.GetType().GetProperty("TestContext");

        if (testContextProperty == null)
            throw new InvalidOperationException("The test class does not have a TestContext property.");

        var testContext = new BasicMsTestContext(methodInfo.Name);
        testContextProperty.SetValue(test, testContext);

        var invocationResult = methodInfo.Invoke(test, null);
        if (invocationResult is Task task)
            await task;
    }
}
