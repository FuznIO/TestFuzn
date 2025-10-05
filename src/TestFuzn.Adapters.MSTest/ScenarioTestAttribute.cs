using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Fuzn.TestFuzn;

[AttributeUsage(AttributeTargets.Method)]
public class ScenarioTestAttribute : TestMethodAttribute
{
    public ScenarioRunMode RunMode { get; internal set; }

    public ScenarioTestAttribute(ScenarioRunMode runMode = ScenarioRunMode.Execute)
    {
        RunMode = runMode;
    }

    public override TestResult[] Execute(ITestMethod testMethod)
    {
        var result = base.Execute(testMethod);
        return result;

        //var firstResult = result.First();

        //if (firstResult.TestFailureException == null
        //    || firstResult.TestFailureException.InnerException == null)
        //    return result;

        //if (firstResult.TestFailureException.InnerException.GetType() == typeof(ScenarioRunModeIgnoreException))
        //{
        //    firstResult.Outcome = UnitTestOutcome.Ignored;
        //    firstResult.
        //    firstResult.TestFailureException = null;

        //    return new TestResult[] { firstResult };
        //}

        //return result;
    }
}
