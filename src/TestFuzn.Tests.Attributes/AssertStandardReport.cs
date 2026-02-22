using Fuzn.TestFuzn.Contracts.Reports;
using Fuzn.TestFuzn.Tests.Attributes.Environments;
using Fuzn.TestFuzn.Tests.Attributes.Skipping;
using Fuzn.TestFuzn.Tests.Attributes.Tags;

namespace Fuzn.TestFuzn.Tests.Attributes;

public class AssertStandardReport : IStandardReport
{
    private StandardReportData _standardReportData = null!;
    public static bool IsExecuted { get; set; }

    Task IStandardReport.WriteReport(StandardReportData standardReportData)
    {
        IsExecuted = true;

        _standardReportData = standardReportData;

        // Environment.
        AssertTestResultStatus<EnvironmentAttributeOnMethodTests>(
           nameof(EnvironmentAttributeOnMethodTests.TestShouldRun), TestStatus.Passed);
        AssertTestResultStatus<EnvironmentAttributeOnClassTests>(
            nameof(EnvironmentAttributeOnClassTests.TestShouldRun), TestStatus.Passed);
        AssertTestResultStatus<EnvironmentAttributeOnClassAndMethodTests>(
            nameof(EnvironmentAttributeOnClassAndMethodTests.TestShouldRun), TestStatus.Passed);
        AssertTestResultStatus<EnvironmentAttributeOnClassAndMethodTests>(
            nameof(EnvironmentAttributeOnClassAndMethodTests.TestShouldNotRun), TestStatus.Skipped);

        // Tags - Include.
        AssertTestResultStatus<TagsAttributeIncludeOnClassTests>(
            nameof(TagsAttributeIncludeOnClassTests.TestShouldRun), TestStatus.Passed);
        AssertTestResultStatus<TagsAttributeIncludeOnMethodTests>(
            nameof(TagsAttributeIncludeOnMethodTests.TestShouldRun), TestStatus.Passed);
        AssertTestResultStatus<TagsAttributeIncludeOnMethodTests>(
            nameof(TagsAttributeIncludeOnMethodTests.TestShouldNotRun), TestStatus.Skipped);
        // Tags - Exclude.
        AssertTestResultStatus<TagsAttributeExcludeOnClassTests>(
            nameof(TagsAttributeExcludeOnClassTests.TestShouldNotRun), TestStatus.Skipped);
        AssertTestResultStatus<TagsAttributeExcludeOnMethodTests>(
            nameof(TagsAttributeExcludeOnMethodTests.TestShouldNotRun), TestStatus.Skipped);

        // Skipped.
        AssertTestResultStatus<SkipAttributeOnClassTests>(
            nameof(SkipAttributeOnClassTests.TestShouldBeSkipped), TestStatus.Skipped);
        AssertTestResultStatus<SkipAttributeOnMethodTests>(
            nameof(SkipAttributeOnMethodTests.TestShouldBeSkipped), TestStatus.Skipped);

        return Task.CompletedTask;
    }

    private void AssertTestResultStatus<TTestClass>(string methodName, TestStatus expectedStatus)
    {
        var methodFullName = GetFullMethodName<TTestClass>(methodName);
        var testResult = GetTestResult(methodFullName);
        Assert.IsNotNull(testResult, $"Test result for method '{methodFullName}' not found.");
        Assert.AreEqual(expectedStatus, testResult.Status, $"Test status for method '{methodFullName}' does not match expected status.");
    }

    private TestFuzn.Contracts.Results.Standard.TestResult? GetTestResult(string methodFullName)
    {
        foreach (var groupResult in _standardReportData.GroupResults)
        {
            foreach (var testResult in groupResult.Value.TestResults)
            {
               if (testResult.Value.FullName == methodFullName)
                {
                    return testResult.Value;
                }
            }
        }
        return null;
    }

    private string GetFullMethodName<TClass>(string methodName)
    {
        var method = typeof(TClass).GetMethod(methodName);
        Assert.IsNotNull(method);
        var fullName = $"{method.DeclaringType!.FullName}.{method.Name}";
        Assert.IsNotNull(fullName);

        return fullName;
    }
}
