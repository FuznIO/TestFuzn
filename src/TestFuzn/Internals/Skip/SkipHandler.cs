using System.Reflection;

namespace Fuzn.TestFuzn;

internal class SkipHandler
{
    public void ApplySkipEvaluation(TestInfo testInfo, MethodInfo testMethod)
    {
        var testSession = TestSession.Current;
        if (testSession == null)
            throw new Exception("TestFuzn must be initialized.");

        var (skip, reason) = Evaluate(testSession, testInfo, testMethod);
        testInfo.Skipped = skip;
        testInfo.SkipReason = reason;

        if (skip)
        {
            testSession.ResultManager.AddSkippedTestResult(testInfo);
        }
    }   

    private (bool Skip, string? Reason) Evaluate(TestSession testSession, TestInfo testInfo, MethodInfo testMethod)
    {
        var skipAttributeResult = EvaluateSkipAttribute(testInfo, testMethod);
        if (skipAttributeResult.Skip)
            return skipAttributeResult;

        var tagsResult = EvaluateTags(testSession, testInfo, testMethod);
        if (tagsResult.Skip)
            return tagsResult;

        var targetEnvResult = EvaluateTargetEnvironment(testSession, testInfo);
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

    private static (bool Skip, string? Reason) EvaluateTags(TestSession testSession, 
        TestInfo test, 
        MethodInfo methodInfo)
    {
        var config = testSession.Configuration;

        if (config.TagsFilterInclude.Count == 0 && config.TagsFilterExclude.Count == 0)
            return (false, null);

        if (test.Tags == null || test.Tags.Count == 0)
        {
            if (config.TagsFilterInclude.Count > 0)
            {
                return (true, $"Test is skipped. Reason: no tags found. Requires all of [{string.Join(", ", config.TagsFilterInclude)}].");
            }

            return (false, null);
        }

        if (config.TagsFilterExclude.Count > 0 &&
            test.Tags.Any(t => config.TagsFilterExclude.Contains(t, StringComparer.OrdinalIgnoreCase)))
        {
            return (true,
                $"Test is skipped. Reason: matching exclude tags. Test tags: [{string.Join(", ", test.Tags)}], Exclude tags: [{string.Join(", ", config.TagsFilterExclude)}]");
        }

        if (config.TagsFilterInclude.Count > 0)
        {
            var missingTags = config.TagsFilterInclude
                .Where(filterTag => !test.Tags.Contains(filterTag, StringComparer.OrdinalIgnoreCase))
                .ToList();

            if (missingTags.Count > 0)
            {
                return (true,
                    $"Test is skipped. Reason: tag mismatch. Test has [{string.Join(", ", test.Tags)}], requires all of [{string.Join(", ", config.TagsFilterInclude)}]. Missing: [{string.Join(", ", missingTags)}].");
            }
        }

        return (false, null);
    }

    private static (bool Skip, string? Reason) EvaluateTargetEnvironment(TestSession testSession,
        TestInfo testInfo)
    {
        var testTargetEnvs = testInfo.TargetEnvironments;
        var currentTargetEnv = testSession.Configuration?.TargetEnvironment ?? "";

        if (testTargetEnvs == null || testTargetEnvs.Count == 0)
        {
            if (string.IsNullOrEmpty(currentTargetEnv))
                return (false, null);
            
            return (true, 
                $"Test is skipped. Reason: test has no target environment specified, but runtime target is [{currentTargetEnv}].");
        }

        if (string.IsNullOrEmpty(currentTargetEnv))
        {
            if (!testTargetEnvs.Any(x => x == ""))
            {
                return (true,
                    $"Test is skipped. Reason: requires target environment [{string.Join(", ", testTargetEnvs)}] but none specified at runtime.");
            }
        }

        if (!testTargetEnvs.Any(x => string.Equals(x, currentTargetEnv, StringComparison.OrdinalIgnoreCase)))
        {
            return (true, 
                $"Test is skipped. Reason: target environment mismatch. Test allows [{string.Join(", ", testTargetEnvs)}], current is [{currentTargetEnv}].");
        }

        return (false, null);
    }
}