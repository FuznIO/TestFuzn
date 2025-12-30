using System.Reflection;
using System.Runtime.CompilerServices;

namespace Fuzn.TestFuzn;

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public class TestAttribute : TestMethodAttribute
{
    public string? Name { get; set; }
    public string? Id { get; set; }
    public string? Description { get; set; }

    public TestAttribute([CallerFilePath] string callerFilePath = "", 
        [CallerLineNumber] int callerLineNumber = -1) : base(callerFilePath, callerLineNumber)
    {
    }

    public override async Task<TestResult[]> ExecuteAsync(ITestMethod testMethod)
    {
        if (string.IsNullOrEmpty(testMethod.TestMethodName))
            throw new Exception("testMethod.TestMethodName is null or empty.");

        var skipBasedOnSkipAttributeResult = ShouldSkipBasedOnSkipAttribute(testMethod);
        if (skipBasedOnSkipAttributeResult.Skip)
            return skipBasedOnSkipAttributeResult.Results;

        var skipBasedOnTagsResult = ShouldSkipBasedOnTags(testMethod);
        if (skipBasedOnTagsResult.Skip)
            return skipBasedOnTagsResult.Results;

        var skipBasedOnEnvironmentResult = ShouldSkipBasedOnEnvironment(testMethod);
        if (skipBasedOnEnvironmentResult.Skip)
            return skipBasedOnEnvironmentResult.Results;

        var result = await base.ExecuteAsync(testMethod);
        return result;
    }

    public FeatureTestInfo GetTestInfo(MethodInfo testMethod)
    {
        var tags = GetTags(testMethod);
        var metadata = GetMetadata(testMethod);
        
        return new FeatureTestInfo
        {
            Name = Name ?? testMethod.Name.Replace("_", ""),
            Description = Description,
            Id = Id,
            Tags = tags,
            Metadata = metadata
        };
    }

    private static List<string>? GetTags(MethodInfo testMethod)
    {
        var tagsAttributes = testMethod.GetCustomAttributes(typeof(TagsAttribute), inherit: true)
                                               .OfType<TagsAttribute>()
                                               .ToList();

        return tagsAttributes.Any() 
            ? tagsAttributes.SelectMany(t => t.Tags ?? []).ToList() 
            : null;
    }

    private static Dictionary<string, string>? GetMetadata(MethodInfo testMethod)
    {
        var metadataAttributes = testMethod.GetCustomAttributes(typeof(MetadataAttribute), inherit: true)
                                                      .OfType<MetadataAttribute>()
                                                      .ToList();

        return metadataAttributes.Any()
            ? metadataAttributes.ToDictionary(m => m.Key, m => m.Value)
            : null;
    }

    private static (bool Skip, TestResult[] Results) ShouldSkipBasedOnSkipAttribute(ITestMethod testMethod)
    {
        var skipAttributes = testMethod.MethodInfo.GetCustomAttributes(typeof(SkipAttribute), inherit: true)
            .OfType<SkipAttribute>()
            .ToList();

        if (!skipAttributes.Any())
            return (false, null);


        return (true, 
            [new() { Outcome = UnitTestOutcome.Ignored, TestFailureException = new Exception($"Test is skipped. Reason: {skipAttributes.First().Reason}") }]);
    }

    private static (bool Skip, TestResult[] Results) ShouldSkipBasedOnTags(ITestMethod testMethod)
    {
        var tagsAttributes = testMethod.MethodInfo.GetCustomAttributes(typeof(TagsAttribute), inherit: true)
                                               .OfType<TagsAttribute>()
                                               .ToList();

        List<string> tags = null;
        if (tagsAttributes.Any())
            tags = tagsAttributes.SelectMany(t => t.Tags ?? []).ToList();

        if (GlobalState.TagsFilterInclude.Count == 0
            && GlobalState.TagsFilterExclude.Count == 0)
        {
            return (false, null);
        }

        if (!tags.Any())
            return (true, [new() { Outcome = UnitTestOutcome.Inconclusive, TestFailureException = new Exception($"Test skipped due to tags mismatch.") }]);

        if (GlobalState.TagsFilterInclude.Count > 0)
        {
            if (!tags.Any(t => GlobalState.TagsFilterInclude.Contains(t, StringComparer.OrdinalIgnoreCase)))
            {
                return (true, [new() { Outcome = UnitTestOutcome.Inconclusive, TestFailureException = new Exception($"Test skipped due to missing include tags. Test tags: [{string.Join(", ", tags)}], Include tags: [{string.Join(", ", GlobalState.TagsFilterInclude)}]") }]);
            }
        }

        if (GlobalState.TagsFilterExclude.Count > 0)
        {
            if (tags.Any(t => GlobalState.TagsFilterExclude.Contains(t, StringComparer.OrdinalIgnoreCase)))
            {
                return (true, [new() { Outcome = UnitTestOutcome.Inconclusive, TestFailureException = new Exception($"Test skipped due to matching exclude tags. Test tags: [{string.Join(", ", tags)}], Exclude tags: [{string.Join(", ", GlobalState.TagsFilterExclude)}]") }]);
            }
        }

        return (false, null);
    }

    public static (bool Skip, TestResult[] Results) ShouldSkipBasedOnEnvironment(ITestMethod testMethod)
    {
        var environmentsAttributes = testMethod.MethodInfo.GetCustomAttributes(typeof(EnvironmentsAttribute), inherit: true)
                                               .OfType<EnvironmentsAttribute>()
                                               .ToList();

        var currentEnvironment = GlobalState.EnvironmentName;

        if (environmentsAttributes.Count > 0)
        {
            var environments = environmentsAttributes.SelectMany(e => e.Environments).ToList();

            if (!environments.Any(x => string.Equals(x, currentEnvironment, StringComparison.OrdinalIgnoreCase)))
                return (true, [new() { Outcome = UnitTestOutcome.Inconclusive, TestFailureException = new Exception($"Test skipped due to environment-mismatch.") }]);
        }
        else
        {
            if (!string.IsNullOrEmpty(currentEnvironment))
                return (true, [new() { Outcome = UnitTestOutcome.Inconclusive, TestFailureException = new Exception($"Test skipped due to environment-mismatch.") }]);

        }

        return (false, null);
    }
}