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

        var targetEnvResult = EvaluateTargetEnvironment(testInfo);
        if (targetEnvResult.Skip)
            return targetEnvResult;

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

        if (test.Tags == null || test.Tags.Count == 0)
            return (false, null);

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
    private static (bool Skip, string? Reason) EvaluateTargetEnvironment(TestInfo testInfo)
    {
        var testTargetEnvs = testInfo.TargetEnvironments;
        var currentTargetEnv = GlobalState.TargetEnvironment ?? "";

        if (testTargetEnvs == null || testTargetEnvs.Count == 0)
        {
            if (string.IsNullOrEmpty(currentTargetEnv))
                return (false, null);
            
            return (true, 
                $"Test skipped: test has no target environment specified, but runtime target is [{currentTargetEnv}].");
        }

        if (string.IsNullOrEmpty(currentTargetEnv))
        {
            if (!testTargetEnvs.Any(x => x == ""))
            {
                return (true,
                    $"Test skipped: requires target environment [{string.Join(", ", testTargetEnvs)}] but none specified at runtime.");
            }
        }

        if (!testTargetEnvs.Any(x => string.Equals(x, currentTargetEnv, StringComparison.OrdinalIgnoreCase)))
        {
            return (true, 
                $"Test skipped: target environment mismatch. Test allows [{string.Join(", ", testTargetEnvs)}], current is [{currentTargetEnv}].");
        }

        return (false, null);
    }
}