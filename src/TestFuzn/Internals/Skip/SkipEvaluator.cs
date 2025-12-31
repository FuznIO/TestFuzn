using System.Reflection;

namespace Fuzn.TestFuzn;

public class SkipEvaluator
{
    public (SkipResult Result, string? Reason) Evaluate(TestInfo testInfo, MethodInfo testMethod)
    {
        var skipAttributeResult = EvaluateSkipAttribute(testInfo, testMethod);
        if (skipAttributeResult.Result != SkipResult.None)
            return skipAttributeResult;

        var tagsResult = EvaluateTags(testInfo, testMethod);
        if (tagsResult.Result != SkipResult.None)
            return tagsResult;

        var environmentResult = EvaluateEnvironment(testInfo, testMethod);
        if (environmentResult.Result != SkipResult.None)
            return environmentResult;

        return (SkipResult.None, null);
    }

    //private static AddToResults(TestInfo testInfo)
    //{
    //    new StandardResult
    //}

    private static (SkipResult Result, string? Reason) EvaluateSkipAttribute(TestInfo testInfo, 
        MethodInfo methodInfo)
    {
        if (!testInfo.HasSkipAttribute)
            return (SkipResult.None, null);

        return (SkipResult.Ignored, $"Test is skipped. Reason: {testInfo.SkipAttributeReason}");
    }

    private static (SkipResult Result, string? Reason) EvaluateTags(TestInfo test, 
        MethodInfo methodInfo)
    {
        if (GlobalState.TagsFilterInclude.Count == 0 && GlobalState.TagsFilterExclude.Count == 0)
            return (SkipResult.None, null);

        if (test.Tags.Count == 0)
            return (SkipResult.Inconclusive, "Test skipped due to tags mismatch.");

        if (GlobalState.TagsFilterInclude.Count > 0 &&
            !test.Tags.Any(t => GlobalState.TagsFilterInclude.Contains(t, StringComparer.OrdinalIgnoreCase)))
        {
            return (SkipResult.Inconclusive,
                $"Test skipped due to missing include tags. Test tags: [{string.Join(", ", test.Tags)}], Include tags: [{string.Join(", ", GlobalState.TagsFilterInclude)}]");
        }

        if (GlobalState.TagsFilterExclude.Count > 0 &&
            test.Tags.Any(t => GlobalState.TagsFilterExclude.Contains(t, StringComparer.OrdinalIgnoreCase)))
        {
            return (SkipResult.Inconclusive,
                $"Test skipped due to matching exclude tags. Test tags: [{string.Join(", ", test.Tags)}], Exclude tags: [{string.Join(", ", GlobalState.TagsFilterExclude)}]");
        }

        return (SkipResult.None, null);
    }

    private static (SkipResult Result, string? Reason) EvaluateEnvironment(TestInfo testInfo,
        MethodInfo methodInfo)
    {
        var currentEnvironment = GlobalState.EnvironmentName;

        if (testInfo.Environments.Count > 0)
        {
            if (!testInfo.Environments.Any(x => string.Equals(x, currentEnvironment, StringComparison.OrdinalIgnoreCase)))
                return (SkipResult.Inconclusive, "Test skipped due to environment-mismatch.");
        }
        else if (!string.IsNullOrEmpty(currentEnvironment))
        {
            return (SkipResult.Inconclusive, "Test skipped due to environment-mismatch.");
        }

        return (SkipResult.None, null);
    }
}