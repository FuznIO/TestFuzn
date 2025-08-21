using System.Reflection;

namespace FuznLabs.TestFuzn.Cli.Internals;

internal class DiscoverScenarioTests
{
    public List<ScenarioTestInfo> GetScenarioTests(Assembly assembly)
    {
        var scenarioTests = new List<ScenarioTestInfo>();

        foreach (var type in assembly.GetTypes())
        {
            if (!type.IsClass)
                continue;

            // Check if the class has an attribute named "TestFusion.FeatureTest"
            var hasFeatureTestAttribute = type
                .GetCustomAttributes(inherit: true)
                .Any(attr => attr.GetType().FullName == "TestFusion.FeatureTestAttribute");

            if (!hasFeatureTestAttribute)
                continue;


            // Find methods with the "TestFusion.ScenarioTest" attribute
            foreach (var method in type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static))
            {
                var hasScenarioTestAttribute = method
                    .GetCustomAttributes(inherit: false)
                    .Any(attr => attr.GetType().FullName == "TestFusion.ScenarioTestAttribute");

                if (!hasScenarioTestAttribute)
                    continue;
                
                var testInfo = new ScenarioTestInfo();
                testInfo.Name = type.FullName + "." +  method.Name;
                testInfo.Class = type;
                testInfo.Method = method;
                scenarioTests.Add(testInfo);
            }
        }

        var ordered = scenarioTests.OrderBy(t => t.Name).ToList();

        return ordered;
    }
}