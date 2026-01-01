using Fuzn.TestFuzn.Internals.Results.Standard;
using System.Reflection;
    
namespace Fuzn.TestFuzn;

public class SkipHandler
{
    private readonly StandardResultManager _resultManager = new();

    public void ApplySkipEvaluation(TestInfo testInfo, MethodInfo testMethod)
    {
        var (skip, reason) = Evaluate(testInfo, testMethod);
        testInfo.Skipped = skip;
        testInfo.SkipReason = reason;

        if (skip)
        {
            _resultManager.AddSkippedTestResult(testInfo);
        }
    }   

    private (bool Skip, string? Reason) Evaluate(TestInfo testInfo, MethodInfo testMethod)
    {
        var skipAttributeResult = EvaluateSkipAttribute(testInfo, testMethod);
        if (skipAttributeResult.Skip)
            return skipAttributeResult;

        var tagsResult = EvaluateTags(testInfo, testMethod);
        if (tagsResult.Skip)
            return tagsResult;

        var environmentResult = EvaluateEnvironment(testInfo, testMethod);
        if (environmentResult.Skip)
            return environmentResult;

        return (false, null);
    }

    private static (bool Skip, string? Reason) EvaluateSkipAttribute(TestInfo testInfo, 
        MethodInfo methodInfo)
    {
        if (!testInfo.HasSkipAttribute)
            return (false, null);

        return (true, $"Test is skipped. Reason: {testInfo.SkipAttributeReason}");
    }

    private static (bool Skip, string? Reason) EvaluateTags(TestInfo test, 
        MethodInfo methodInfo)
    {
        if (GlobalState.TagsFilterInclude.Count == 0 && GlobalState.TagsFilterExclude.Count == 0)
            return (false, null);

        if (test.Tags.Count == 0)
            return (false, "Test skipped due to tags mismatch.");

        if (GlobalState.TagsFilterInclude.Count > 0 &&
            !test.Tags.Any(t => GlobalState.TagsFilterInclude.Contains(t, StringComparer.OrdinalIgnoreCase)))
        {
            return (true,
                $"Test skipped due to missing include tags. Test tags: [{string.Join(", ", test.Tags)}], Include tags: [{string.Join(", ", GlobalState.TagsFilterInclude)}]");
        }

        if (GlobalState.TagsFilterExclude.Count > 0 &&
            test.Tags.Any(t => GlobalState.TagsFilterExclude.Contains(t, StringComparer.OrdinalIgnoreCase)))
        {
            return (true,
                $"Test skipped due to matching exclude tags. Test tags: [{string.Join(", ", test.Tags)}], Exclude tags: [{string.Join(", ", GlobalState.TagsFilterExclude)}]");
        }

        return (false, null);
    }

    private static (bool Skip, string? Reason) EvaluateEnvironment(TestInfo testInfo,
        MethodInfo methodInfo)
    {
        var currentEnvironment = GlobalState.EnvironmentName;

        if (testInfo.Environments.Count > 0)
        {
            if (!testInfo.Environments.Any(x => string.Equals(x, currentEnvironment, StringComparison.OrdinalIgnoreCase)))
                return (true, "Test skipped due to environment-mismatch.");
        }
        else if (!string.IsNullOrEmpty(currentEnvironment))
        {
            return (true, "Test skipped due to environment-mismatch.");
        }

        return (false, null);
    }
}