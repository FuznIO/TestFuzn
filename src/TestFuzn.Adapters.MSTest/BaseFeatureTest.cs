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
        var scenario = new ScenarioBuilder<EmptyModel>(new MsTestAdapter(TestContext), this, scenarioName);
        ApplyTestCategoryTags(scenario, scenarioName);
        return scenario;
    }

    public ScenarioBuilder<TModel> Scenario<TModel>([CallerMemberName] string scenarioName = null)
        where TModel : new()
    {
        var scenario = new ScenarioBuilder<TModel>(new MsTestAdapter(TestContext), this, scenarioName);
        ApplyTestCategoryTags(scenario, scenarioName);
        return scenario;
    }

    private void ApplyTestCategoryTags<TModel>(ScenarioBuilder<TModel> scenarioBuilder, string methodName)
        where TModel : new()
    {
        if (string.IsNullOrWhiteSpace(methodName))
            return;

        // Look for the test method on this test class (public or non-public instance)
        var method = GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (method == null)
            return;

        // MSTest allows multiple [TestCategory] attributes
        var categoryAttributes = method.GetCustomAttributes(typeof(TestCategoryAttribute), inherit: true)
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


}
