using Fuzn.TestFuzn.Attributes;
using System.Runtime.CompilerServices;

namespace Fuzn.TestFuzn;

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public class TestAttribute : TestMethodAttribute
{
    public string? Name { get; set; }
    public string? Id { get; set; }
    public string? Description { get; set; }
    public TestAttribute([CallerFilePath] string callerFilePath = "", 
        [CallerLineNumber] int callerLineNumber = -1)
    {
    }

    public override async Task<TestResult[]> ExecuteAsync(ITestMethod testMethod)
    {
        if (string.IsNullOrEmpty(testMethod.TestMethodName))
            throw new Exception("testMethod.TestMethodName is null or empty.");

        try
        {
            HandleScenarioIgnore(testMethod);
        }
        catch (Exception ex)
        {
            return
            [
                new TestResult
                {
                    Outcome = UnitTestOutcome.Ignored,
                    TestFailureException = ex
                }
            ];
        }

        try
        {
            EnsureTestCategoryAttributeIsNotUsed(testMethod);
            EnsureDataRowAndDataSourceAttributesAreNotUsed(testMethod);
            EnsureScenarioTestMatchesTagFilters(testMethod);
        }
        catch (Exception ex)
        {
            return
            [
                new TestResult
                {
                    Outcome = UnitTestOutcome.Inconclusive,
                    TestFailureException = ex
                }
            ];
        }

        var result = await base.ExecuteAsync(testMethod);
        return result;
    }

    private static void HandleScenarioIgnore(ITestMethod testMethod)
    {
        var ignoreAttributes = testMethod.MethodInfo.GetCustomAttributes(typeof(SkipAttribute), inherit: true)
            .OfType<SkipAttribute>()
            .ToList();

        if (ignoreAttributes.Any())
        {
            var reason = ignoreAttributes.First().Reason;
            throw new Exception($"Scenario is ignored. Reason: {reason}");
        }
    }

    private static void EnsureDataRowAndDataSourceAttributesAreNotUsed(ITestMethod testMethod)
    {
        var dataRowAttributes = testMethod.MethodInfo.GetCustomAttributes(typeof(DataRowAttribute), inherit: true)
                                           .OfType<DataRowAttribute>()
                                           .ToList();
        if (dataRowAttributes.Any())
        {
            throw new Exception("MSTest DataRowAttribute is not supported. Please use TestFuzn's DataDrivenTestAttribute instead.");
        }
        var dataSourceAttributes = testMethod.MethodInfo.GetCustomAttributes(typeof(DataSourceAttribute), inherit: true)
                                           .OfType<DataSourceAttribute>()
                                           .ToList();
        if (dataSourceAttributes.Any())
        {
            throw new Exception("MSTest DataSourceAttribute is not supported. Please use TestFuzn's DataDrivenTestAttribute instead.");
        }
    }

    private static void EnsureTestCategoryAttributeIsNotUsed(ITestMethod testMethod)
    {
        var classCategoryAttributes = testMethod.MethodInfo.DeclaringType?.GetCustomAttributes(typeof(TestCategoryAttribute), inherit: true)
                                               .OfType<TestCategoryAttribute>()
                                               .ToList();

        if (classCategoryAttributes.Any())
            throw new Exception($"MSTest TestCategoryAttribute is not supported. Please use {typeof(TagsAttribute)} instead.");

        var methodCategoryAttributes = testMethod.MethodInfo.GetCustomAttributes(typeof(TestCategoryAttribute), inherit: true)
                                               .OfType<TestCategoryAttribute>()
                                               .ToList();

        if (methodCategoryAttributes.Any())
            throw new Exception($"MSTest TestCategoryAttribute is not supported. Please use {typeof(TagsAttribute)} instead.");
    }

    private static void EnsureScenarioTestMatchesTagFilters(ITestMethod testMethod)
    {
        var classTagsAttributes = testMethod.MethodInfo.DeclaringType?.GetCustomAttributes(typeof(TagsAttribute), inherit: true)
                                               .OfType<TagsAttribute>()
                                               .ToList();

        var methodTagsAttributes = testMethod.MethodInfo.GetCustomAttributes(typeof(TagsAttribute), inherit: true)
                                               .OfType<TagsAttribute>()
                                               .ToList();

        var allTagsAttributes = methodTagsAttributes.Concat(classTagsAttributes).ToList();

        if (allTagsAttributes.Count > 0)
        {
            var tags = allTagsAttributes
                .SelectMany(a => a.Tags ?? [])
                .Where(c => !string.IsNullOrWhiteSpace(c))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (tags.Count > 0)
            {
                if (GlobalState.TagsFilterInclude.Count > 0)
                {
                    if (!tags.Any(t => GlobalState.TagsFilterInclude.Contains(t, StringComparer.OrdinalIgnoreCase)))
                    {
                        throw new Exception($"ScenarioTest skipped due to missing include tags. Test tags: [{string.Join(", ", tags)}], Include tags: [{string.Join(", ", GlobalState.TagsFilterInclude)}]");
                    }
                }

                if (GlobalState.TagsFilterExclude.Count > 0)
                {
                    if (tags.Any(t => GlobalState.TagsFilterExclude.Contains(t, StringComparer.OrdinalIgnoreCase)))
                    {
                        throw new Exception($"ScenarioTest skipped due to matching exclude tags. Test tags: [{string.Join(", ", tags)}], Exclude tags: [{string.Join(", ", GlobalState.TagsFilterExclude)}]");
                    }
                }
            }
        }
        else
        {
            if (GlobalState.TagsFilterInclude.Count > 0)
            {
                throw new Exception($"Test skipped due to missing include tags. ScenarioTest has no tags, Include tags: [{string.Join(", ", GlobalState.TagsFilterInclude)}]");
            }
        }
    }
}
