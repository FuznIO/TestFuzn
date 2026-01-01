using System.Reflection;
using System.Runtime.CompilerServices;

namespace Fuzn.TestFuzn;

public abstract class Test : ITest
{
    public object TestFramework
    {
        get
        {
            if (field == null)
                field = new MsTestRunnerAdapter(TestContext);
            return field;
        }
        set
        {
            field = value;
        }
    }

    public MethodInfo TestMethodInfo
    {
        get
        {
            if (field == null)
            {
                if (string.IsNullOrEmpty(TestContext.TestName))
                    throw new Exception("TestContext.TestName is null or empty.");

                var methodInfo = GetType().GetMethod(TestContext.TestName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (methodInfo == null)
                    throw new Exception("Test method not found.");

                field = methodInfo;
            }

            return field;
        }
        set
        {
            field = value;
        }
    }
    public TestInfo TestInfo { get; set; }
    public TestContext TestContext { get; set; }

    protected Test()
    {
        var className = GetType().FullName ?? GetType().Name;
        className = className.Replace('_', ' ');
        if (className.EndsWith("Tests"))
            className = className.Substring(0, className.Length - 5);
        else if (className.EndsWith("Test"))
            className = className.Substring(0, className.Length - 4);
    }

    public ScenarioBuilder<EmptyModel> Scenario([CallerMemberName] string scenarioName = null)
    {
        return Scenario<EmptyModel>(scenarioName);
    }

    public ScenarioBuilder<TModel> Scenario<TModel>([CallerMemberName] string scenarioName = "")
        where TModel : new()
    {
        var testMethod = TestMethodInfo;
        
        if (TestInfo == null)
            TestInfo = GetTestInfoFromTestAttribute(testMethod);

        var scenario = new ScenarioBuilder<TModel>(TestFramework, this, scenarioName);

        return scenario;
    }

    private TestInfo GetTestInfoFromTestAttribute(MethodInfo methodInfo)
    {
        // Check for [Test] attribute.
        var testAttribute = methodInfo.GetCustomAttributes(typeof(TestAttribute), inherit: true)
                                .OfType<TestAttribute>()
                                .FirstOrDefault();

        if (testAttribute == null)
            throw new InvalidOperationException($"Method '{methodInfo.Name}' must be decorated with [Test] attribute.");

        return testAttribute.GetTestInfo(methodInfo);
    }
}
