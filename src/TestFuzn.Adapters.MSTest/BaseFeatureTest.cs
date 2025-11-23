using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace Fuzn.TestFuzn;

public abstract class BaseFeatureTest : IFeatureTest
{
    public TestContext TestContext { get; set; }
    public virtual string FeatureName { get; set; }
    public virtual string FeatureId { get; set; }
    public virtual Dictionary<string, string> FeatureMetadata { get; set; } = new();

    protected BaseFeatureTest()
    {
        var featureName = GetType().Name.Replace('_', ' ');
        if (featureName.EndsWith("Tests"))
            featureName = featureName.Substring(0, featureName.Length - 5);
        else if (featureName.EndsWith("Test"))
            featureName = featureName.Substring(0, featureName.Length - 4);
            
        FeatureName = featureName;
    }

    public virtual Task InitTestMethod(Context context)
    {
        return Task.CompletedTask;
    }

    public virtual Task CleanupTestMethod(Context context)
    {
        return Task.CompletedTask;
    }

    public ScenarioBuilder<EmptyModel> Scenario([CallerMemberName] string scenarioName = null)
    {
        return Scenario<EmptyModel>(scenarioName);
    }

    public ScenarioBuilder<TModel> Scenario<TModel>([CallerMemberName] string scenarioName = "")
        where TModel : new()
    {
        if (string.IsNullOrEmpty(TestContext?.TestName))
            throw new Exception("TestContext.TestName is null or empty.");

        var methodInfo = GetType().GetMethod(TestContext.TestName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (methodInfo == null)
            throw new Exception("Test method not found.");

        EnsureTestMethodIsNotUsed(methodInfo);

        var scenario = new ScenarioBuilder<TModel>(new MsTestAdapter(TestContext), this, scenarioName);
        EnsureScenarioTestAndApplyRunMode(methodInfo, scenario);
        ApplyTestCategoryTags(methodInfo, scenario);
        ApplyEnvironments(methodInfo, scenario);
        return scenario;
    }

    private void EnsureTestMethodIsNotUsed(MethodInfo methodInfo)
    {
        // Check for MSTest [TestMethod] attribute
        var hasTestMethod = methodInfo.GetCustomAttributes(inherit: true)
            .Any(a => a.GetType() == typeof(TestMethodAttribute));
        if (hasTestMethod)
            throw new InvalidOperationException($"Method '{methodInfo.Name}' uses [TestMethod]. Use [ScenarioTest] instead for scenario-based tests.");
    }

    private void EnsureScenarioTestAndApplyRunMode<TModel>(MethodInfo methodInfo, ScenarioBuilder<TModel> scenario)
        where TModel : new()
    {
        // Look for [ScenarioTest] attribute.
        var scenarioTestAttr = methodInfo.GetCustomAttributes(inherit: true)
                                     .FirstOrDefault(a => a.GetType() == typeof(ScenarioTestAttribute));
        if (scenarioTestAttr == null)
            throw new InvalidOperationException($"Scenario method '{methodInfo.Name}' must be decorated with [ScenarioTest] attribute.");

        // Get RunMode property from the attribute.
        var runModeProp = scenarioTestAttr.GetType().GetProperty("RunMode");

        var runModeValue = (ScenarioRunMode) runModeProp.GetValue(scenarioTestAttr);

        // Set it on the scenario.
        if (runModeValue == ScenarioRunMode.Skip)
            scenario.Skip();
    }

    private void ApplyTestCategoryTags<TModel>(MethodInfo methodInfo, ScenarioBuilder<TModel> scenarioBuilder)
        where TModel : new()
    {
        // MSTest allows multiple [TestCategory] attributes
        List<TestCategoryAttribute> categoryAttributes = methodInfo.GetCustomAttributes(typeof(TestCategoryAttribute), inherit: true)
                                       .OfType<TestCategoryAttribute>()
                                       .ToList();
        if (categoryAttributes.Count == 0)
            return;

        var categories = categoryAttributes
            .SelectMany(a => a.TestCategories ?? [])
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (categories.Length > 0)
            scenarioBuilder.Tags(categories);
    }

    private void ApplyEnvironments<TModel>(MethodInfo methodInfo, ScenarioBuilder<TModel> scenario) where TModel : new()
    {
        // Look for [Environments] attribute.
        var environmentsAttr = methodInfo.GetCustomAttributes(inherit: true)
                                     .FirstOrDefault(a => a.GetType() == typeof(EnvironmentsAttribute));
        if (environmentsAttr == null)
            return;

        // Get RunMode property from the attribute.
        var environmentsProp = environmentsAttr.GetType().GetProperty("Environments");

        var environments = (string[]) environmentsProp.GetValue(environmentsAttr);

        scenario.Environments(environments);
    }
}
