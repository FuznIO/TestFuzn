using System.Reflection;

namespace Fuzn.TestFuzn.Cli.Internals;

internal class ScenarioTestInfo
{
    public string Name { get; set; }
    public Type Class { get; set; }
    public MethodInfo Method { get; set; }
}