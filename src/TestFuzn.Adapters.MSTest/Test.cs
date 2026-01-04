using System.Reflection;
using System.Runtime.CompilerServices;

namespace Fuzn.TestFuzn;

/// <summary>
/// Abstract base class for test classes using the MSTest adapter.
/// Inherit from this class to create TestFuzn tests with MSTest.
/// </summary>
public abstract class Test : ITest
{
    /// <inheritdoc/>
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

    /// <inheritdoc/>
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

    /// <inheritdoc/>
    public TestInfo TestInfo { get; set; }

    /// <summary>
    /// Gets or sets the MSTest test context.
    /// </summary>
    public TestContext TestContext { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="Test"/> class.
    /// </summary>
    protected Test()
    {
    }

    /// <summary>
    /// Creates a new scenario builder using the default <see cref="EmptyModel"/> for data sharing.
    /// </summary>
    /// <param name="scenarioName">The name of the scenario. Defaults to the test name for tests with a single scenario.</param>
    /// <returns>A <see cref="ScenarioBuilder{TModel}"/> instance for configuring and running the scenario.</returns>
    public ScenarioBuilder<EmptyModel> Scenario([CallerMemberName] string scenarioName = null)
    {
        return Scenario<EmptyModel>(scenarioName);
    }

    /// <summary>
    /// Creates a new scenario builder with a custom model type for sharing data across steps.
    /// </summary>
    /// <typeparam name="TModel">The model type used to share data across steps within an iteration.</typeparam>
    /// <param name="scenarioName">The name of the scenario. Defaults to the test name for tests with a single scenario.</param>
    /// <returns>A <see cref="ScenarioBuilder{TModel}"/> instance for configuring and running the scenario.</returns>
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
