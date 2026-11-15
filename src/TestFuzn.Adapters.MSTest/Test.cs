using System.ComponentModel;
using System.Reflection;
using Fuzn.TestFuzn.Adapters;
using Fuzn.TestFuzn.Internals;

namespace Fuzn.TestFuzn;

/// <summary>
/// Abstract base class for test classes using the MSTest adapter.
/// Inherit from this class to create TestFuzn tests with MSTest.
/// </summary>
/// <remarks>
/// To run code at a point in the lifecycle, implement the matching hook interface:
/// <list type="table">
///   <listheader><term>Interface</term><description>Runs</description></listheader>
///   <item><term><see cref="IBeforeTest"/></term><description>Before each test. Implement on the test class.</description></item>
///   <item><term><see cref="IAfterTest"/></term><description>After each test. Implement on the test class.</description></item>
///   <item><term><see cref="IBeforeClass"/></term><description>Once before the first test in the class. Implement on the test class.</description></item>
///   <item><term><see cref="IAfterClass"/></term><description>Once after the last test in the class. Implement on the test class.</description></item>
///   <item><term><see cref="IBeforeSuite"/></term><description>Once before any test runs. Implement on the startup class.</description></item>
///   <item><term><see cref="IAfterSuite"/></term><description>Once after all tests end. Implement on the startup class.</description></item>
/// </list>
/// <example>
/// <code>
/// public class OrderTests : Test, IBeforeTest, IAfterTest
/// {
///     public Task BeforeTest(Context context) => Task.CompletedTask;
///     public Task AfterTest(Context context) => Task.CompletedTask;
/// }
/// </code>
/// </example>
/// </remarks>
public abstract class Test : ITest
{
    /// <summary>
    /// Called by MSTest before any test in a derived class runs.
    /// If the class implements <see cref="IBeforeClass"/>, invokes <see cref="IBeforeClass.BeforeClass"/>.
    /// </summary>
    [ClassInitialize(InheritanceBehavior.BeforeEachDerivedClass)]
    public static async Task TestFuznClassInitialize(TestContext testContext)
    {
        MsTestSessionHandler.EnsureInitialized();

        var testFramework = new MsTestRunnerAdapter(testContext);
        var testSession = GetTestSessionAndEnsureInitialized(testFramework);

        var classType = ResolveTestClassType(testSession, testContext.FullyQualifiedTestClassName);
        if (classType == null || !typeof(IBeforeClass).IsAssignableFrom(classType))
            return;

        var instance = (IBeforeClass) Activator.CreateInstance(classType)!;
        var context = ContextFactory.CreateContext(testSession, testSession.ServiceProvider, testFramework, "BeforeClass");
        await instance.BeforeClass(context);
    }

    /// <summary>
    /// Called by MSTest after all tests in a derived class complete.
    /// If the class implements <see cref="IAfterClass"/>, invokes <see cref="IAfterClass.AfterClass"/>.
    /// </summary>
    [ClassCleanup(InheritanceBehavior.BeforeEachDerivedClass)]
    public static async Task TestFuznClassCleanup(TestContext testContext)
    {
        // The class init already failed with the Init error. Do not report it a second time.
        if (MsTestSessionHandler.InitFailed)
            return;

        var testFramework = new MsTestRunnerAdapter(testContext);
        var testSession = GetTestSessionAndEnsureInitialized(testFramework);

        var classType = ResolveTestClassType(testSession, testContext.FullyQualifiedTestClassName);
        if (classType == null || !typeof(IAfterClass).IsAssignableFrom(classType))
            return;

        var instance = (IAfterClass) Activator.CreateInstance(classType)!;
        var context = ContextFactory.CreateContext(testSession, testSession.ServiceProvider, testFramework, "AfterClass");
        await instance.AfterClass(context);
    }

    /// <summary>
    /// Called by MSTest after each test. Fails a passed test that built a scenario but never awaited it.
    /// </summary>
    [TestCleanup]
    public void TestFuznTestCleanup()
    {
        if (TestContext.CurrentTestOutcome != UnitTestOutcome.Passed)
            return;

        ScenarioRunGuard.ThrowIfNotRun(TestInfo);
    }

    private static TestSession GetTestSessionAndEnsureInitialized(MsTestRunnerAdapter testFramework)
    {
        var testSession = TestSession.Current ?? TestSession.Default;
        if (testSession == null)
            throw new InvalidOperationException(MsTestSessionHandler.NotHostedMessage);

        testSession.EnsureInitialized(testFramework);
        return testSession;
    }

    private static Type? ResolveTestClassType(TestSession testSession, string? fullyQualifiedClassName)
    {
        if (string.IsNullOrEmpty(fullyQualifiedClassName))
            return null;

        var testAssembly = testSession.StartupInstance.GetType().Assembly;
        return testAssembly.GetType(fullyQualifiedClassName);
    }

    /// <inheritdoc/>
    [EditorBrowsable(EditorBrowsableState.Never)]
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
    [EditorBrowsable(EditorBrowsableState.Never)]
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
    [EditorBrowsable(EditorBrowsableState.Never)]
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
    /// <param name="scenarioName">The scenario name. In a standard test it is the test name, unless [Test(Name = ...)] is set. Defaults to the test name.</param>
    /// <returns>A <see cref="ScenarioBuilder{TModel}"/> instance for configuring and running the scenario.</returns>
    public ScenarioBuilder<EmptyModel> Scenario(string? scenarioName = null)
    {
        return Scenario<EmptyModel>(scenarioName);
    }

    /// <summary>
    /// Creates a new scenario builder with a custom model type for sharing data across steps.
    /// </summary>
    /// <typeparam name="TModel">The model type used to share data across steps within an iteration.</typeparam>
    /// <param name="scenarioName">The scenario name. In a standard test it is the test name, unless [Test(Name = ...)] is set. Defaults to the test name.</param>
    /// <returns>A <see cref="ScenarioBuilder{TModel}"/> instance for configuring and running the scenario.</returns>
    public ScenarioBuilder<TModel> Scenario<TModel>(string? scenarioName = null)
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
