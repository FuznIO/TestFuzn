using System.Reflection;
using System.Runtime.CompilerServices;

namespace Fuzn.TestFuzn;

/// <summary>
/// Marks a method as a TestFuzn test method.
/// This attribute extends MSTest's TestMethodAttribute and provides additional metadata for TestFuzn.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public class TestAttribute : TestMethodAttribute
{
    /// <summary>
    /// Gets or sets the display name of the test.
    /// If not specified, the method name is used with underscores replaced by spaces.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// Gets or sets the unique identifier for the test. This can be used for tracking and reporting purposes if a test is renamed.
    /// </summary>
    public string? Id { get; set; }

    /// <summary>
    /// Gets or sets the description of the test.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="TestAttribute"/> class.
    /// </summary>
    /// <param name="callerFilePath">The source file path of the caller. Automatically populated by the compiler.</param>
    /// <param name="callerLineNumber">The line number in the source file. Automatically populated by the compiler.</param>
    public TestAttribute([CallerFilePath] string callerFilePath = "", 
        [CallerLineNumber] int callerLineNumber = -1) : base(callerFilePath, callerLineNumber)
    {
    }

    /// <inheritdoc/>
    public override async Task<TestResult[]> ExecuteAsync(ITestMethod testMethod)
    {
        if (string.IsNullOrEmpty(testMethod.TestMethodName))
            throw new Exception("testMethod.TestMethodName is null or empty.");

        var testInfo = GetTestInfo(testMethod.MethodInfo);

        new SkipHandler().ApplySkipEvaluation(testInfo, testMethod.MethodInfo);

        if (testInfo.Skipped)
        {
            return [new()
            {
                Outcome = UnitTestOutcome.Ignored,
                TestFailureException = new Exception(testInfo.SkipReason)
            }];
        }

        var result = await base.ExecuteAsync(testMethod);
        return result;
    }

    internal TestInfo GetTestInfo(MethodInfo methodInfo)
    {
        var testInfo = new TestInfo();
        testInfo.Name = methodInfo.Name.Replace("_", " ");
        testInfo.FullName = methodInfo.DeclaringType.FullName + "." + methodInfo.Name;
        testInfo.Description = Description;
        testInfo.Id = Id;
        testInfo.Group = GetGroupInfo(methodInfo);
        testInfo.Tags = GetTags(methodInfo);
        testInfo.Metadata = GetMetadata(methodInfo);
        testInfo.TargetEnvironments = GetTargetEnvironments(methodInfo);
        var skipAttributeInfo = GetSkipAttributeInfo(methodInfo);
        testInfo.HasSkipAttribute = skipAttributeInfo.HasSkipAttribute;
        testInfo.SkipAttributeReason = skipAttributeInfo.Reason;

        return testInfo;
    }

    private static (bool HasSkipAttribute, string Reason) GetSkipAttributeInfo(MethodInfo testMethod)
    {
        var skipAttribute = testMethod.GetCustomAttributes<SkipAttribute>(inherit: true)
                                      .FirstOrDefault()
                           ?? testMethod.DeclaringType?
                                      .GetCustomAttributes<SkipAttribute>(inherit: true)
                                      .FirstOrDefault();
        if (skipAttribute != null)
        {
            return (true, skipAttribute.Reason);
        }
        return (false, string.Empty);
    }

    private static List<string>? GetTags(MethodInfo testMethod)
    {
        var methodTags = testMethod.GetCustomAttributes<TagsAttribute>(inherit: true);
        var classTags = testMethod.DeclaringType?
            .GetCustomAttributes<TagsAttribute>(inherit: true) ?? [];

        var tagsAttributes = methodTags.Concat(classTags).ToList();

        return tagsAttributes.Any() 
            ? tagsAttributes.SelectMany(t => t.Tags ?? []).ToList() 
            : null;
    }

    private static Dictionary<string, string>? GetMetadata(MethodInfo testMethod)
    {
        var methodMetadata = testMethod.GetCustomAttributes<MetadataAttribute>(inherit: true);
        var classMetadata = testMethod.DeclaringType?
            .GetCustomAttributes<MetadataAttribute>(inherit: true) ?? [];

        var metadataAttributes = methodMetadata.Concat(classMetadata).ToList();

        return metadataAttributes.Any()
            ? metadataAttributes.ToDictionary(m => m.Key, m => m.Value)
            : null;
    }

    private static List<string> GetTargetEnvironments(MethodInfo methodInfo)
    {
        var methodEnvs = methodInfo.GetCustomAttributes<TargetEnvironmentsAttribute>(inherit: true);
        var classEnvs = methodInfo.DeclaringType?
            .GetCustomAttributes<TargetEnvironmentsAttribute>(inherit: true) ?? [];

        var targetEnvAttributes = methodEnvs.Concat(classEnvs).ToList();

        return targetEnvAttributes.Any()
            ? targetEnvAttributes.SelectMany(e => e.Environments ?? []).ToList()
            : [];
    }

    private static GroupInfo GetGroupInfo(MethodInfo methodInfo)
    {
        string groupName = "";
        var declaringType = methodInfo.DeclaringType;

        if (declaringType != null)
        {
            var groupAttribute = declaringType.GetCustomAttribute<GroupAttribute>(inherit: false);

            if (groupAttribute == null)
            {
                groupName = declaringType.FullName ?? declaringType.Name;
                groupName = groupName.Replace('_', ' ');
                if (groupName.EndsWith("Tests"))
                    groupName = groupName.Substring(0, groupName.Length - 5);
                else if (groupName.EndsWith("Test"))
                    groupName = groupName.Substring(0, groupName.Length - 4);
            }
            else
            {
                groupName = groupAttribute.Name;
            }
        }

        return new GroupInfo { Name = groupName };
    }
}