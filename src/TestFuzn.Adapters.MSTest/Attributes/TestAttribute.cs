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

    public TestInfo GetTestInfo(MethodInfo methodInfo)
    {
        var testInfo = new TestInfo();
        testInfo.Name = methodInfo.Name.Replace("_", " ");
        testInfo.FullName = methodInfo.DeclaringType.FullName + "." + methodInfo.Name;
        testInfo.Description = Description;
        testInfo.Id = Id;
        testInfo.Group = GetGroupInfo(methodInfo);
        testInfo.Tags = GetTags(methodInfo);
        testInfo.Metadata = GetMetadata(methodInfo);
        testInfo.Environments = GetEnvironments(methodInfo);
        var skipAttributeInfo = GetSkipAttributeInfo(methodInfo);
        testInfo.HasSkipAttribute = skipAttributeInfo.HasSkipAttribute;
        testInfo.SkipAttributeReason = skipAttributeInfo.Reason;

        return testInfo;
    }

    private static (bool HasSkipAttribute, string Reason) GetSkipAttributeInfo(MethodInfo testMethod)
    {
        var skipAttribute = testMethod.GetCustomAttributes<SkipAttribute>(inherit: true)
                                      .FirstOrDefault();
        if (skipAttribute != null)
        {
            return (true, skipAttribute.Reason);
        }
        return (false, string.Empty);
    }

    private static List<string>? GetTags(MethodInfo testMethod)
    {
        var tagsAttributes = testMethod.GetCustomAttributes<TagsAttribute>(inherit: true)
                                               .ToList();

        return tagsAttributes.Any() 
            ? tagsAttributes.SelectMany(t => t.Tags ?? []).ToList() 
            : null;
    }

    private static Dictionary<string, string>? GetMetadata(MethodInfo testMethod)
    {
        var metadataAttributes = testMethod.GetCustomAttributes<MetadataAttribute>(inherit: true)
                                                      .ToList();

        return metadataAttributes.Any()
            ? metadataAttributes.ToDictionary(m => m.Key, m => m.Value)
            : null;
    }

    private static List<string> GetEnvironments(MethodInfo methodInfo)
    {
        var environmentsAttributes = methodInfo.GetCustomAttributes<EnvironmentsAttribute>(inherit: true)
                                     .ToList();

        return environmentsAttributes.Any()
            ? environmentsAttributes.SelectMany(e => e.Environments ?? []).ToList()
            : new();
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