using System.Reflection;

namespace Fuzn.TestFuzn;

[AttributeUsage(AttributeTargets.Method)]
public class ScenarioTestAttribute : TestMethodAttribute
{
    public ScenarioRunMode RunMode { get; internal set; }

    public ScenarioTestAttribute(ScenarioRunMode runMode = ScenarioRunMode.Execute)
    {
        RunMode = runMode;
    }

    public override async Task<TestResult[]> ExecuteAsync(ITestMethod testMethod)
    {
        if (string.IsNullOrEmpty(testMethod.TestMethodName))
            throw new Exception("TestContext.TestName is null or empty.");

        List<TestCategoryAttribute> tagsAttributes = testMethod.MethodInfo.GetCustomAttributes(typeof(TestCategoryAttribute), inherit: true)
                                       .OfType<TestCategoryAttribute>()
                                       .ToList();

        if (tagsAttributes.Count > 0)
        {
            var tags = tagsAttributes
                .SelectMany(a => a.TestCategories ?? [])
                .Where(c => !string.IsNullOrWhiteSpace(c))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (tags.Count > 0)
            {
                if (GlobalState.TagsFilterInclude.Count > 0)
                {
                    if (!tags.Any(t => GlobalState.TagsFilterInclude.Contains(t, StringComparer.OrdinalIgnoreCase)))
                    {
                        return
                        [
                            new TestResult
                            {
                                Outcome = UnitTestOutcome.Inconclusive,
                                TestFailureException = new Exception($"ScenarioTest skipped due to missing include tags. Test tags: [{string.Join(", ", tags)}], Include tags: [{string.Join(", ", GlobalState.TagsFilterInclude)}]")
                            }
                        ];
                    }
                }

                if (GlobalState.TagsFilterExclude.Count > 0)
                {
                    if (tags.Any(t => GlobalState.TagsFilterExclude.Contains(t, StringComparer.OrdinalIgnoreCase)))
                    {
                        return
                        [
                            new TestResult
                            {
                                Outcome = UnitTestOutcome.Inconclusive,
                                TestFailureException = new Exception($"ScenarioTest skipped due to matching exclude tags. Test tags: [{string.Join(", ", tags)}], Exclude tags: [{string.Join(", ", GlobalState.TagsFilterExclude)}]")
                            }
                        ];
                    }
                }
            }
        }
        else
        {
            if (GlobalState.TagsFilterInclude.Count > 0)
            {
                return
                [
                    new TestResult
                    {
                        Outcome = UnitTestOutcome.Inconclusive,
                        TestFailureException = new Exception($"Test skipped due to missing include tags. ScenarioTest has no tags, Include tags: [{string.Join(", ", GlobalState.TagsFilterInclude)}]")
                    }
                ];
            }
        }

        var result = await base.ExecuteAsync(testMethod);
        return result;
    }
}
