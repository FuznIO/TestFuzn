using System.Reflection;

namespace Fuzn.TestFuzn.StandaloneRunner;

internal class DiscoverTests
{
    public List<DiscoveredTest> GetTests(Assembly assembly)
    {
        var scenarioTests = new List<DiscoveredTest>();

        foreach (var type in assembly.GetTypes())
        {
            if (!type.IsClass)
                continue;

            if (!typeof(ITest).IsAssignableFrom(type))
                continue;

            // Find methods with the "Test" attribute
            foreach (var method in type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static))
            {
                var hasTestAttribute = method
                    .GetCustomAttributes(inherit: false)
                    .Any(attr => attr.GetType().FullName == "Fuzn.TestFuzn.TestAttribute");

                if (!hasTestAttribute)
                    continue;
                
                var test = new DiscoveredTest();
                test.Name = type.FullName + "." +  method.Name;
                test.Class = type;
                test.Method = method;
                scenarioTests.Add(test);
            }
        }

        var ordered = scenarioTests.OrderBy(t => t.Name).ToList();

        return ordered;
    }
}