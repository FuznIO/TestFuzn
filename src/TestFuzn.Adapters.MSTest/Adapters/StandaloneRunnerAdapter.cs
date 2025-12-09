using Fuzn.TestFuzn.StandaloneRunner;
using System.Reflection;

namespace Fuzn.TestFuzn.Adapters;

internal class StandaloneRunnerAdapter : BaseStandaloneRunnerAdapter
{
    public override async Task ExecuteTestMethod(IFeatureTest featureTest, MethodInfo methodInfo)
    {
        var testContextProperty = featureTest.GetType().GetProperty("TestContext");

        if (testContextProperty == null)
            throw new InvalidOperationException("The feature test does not have a TestContext property.");

        var testContext = new BasicMsTestContext(methodInfo.Name);
        testContextProperty.SetValue(featureTest, testContext);

        var invocationResult = methodInfo.Invoke(featureTest, null);
        if (invocationResult is Task task)
            await task;
    }
}
