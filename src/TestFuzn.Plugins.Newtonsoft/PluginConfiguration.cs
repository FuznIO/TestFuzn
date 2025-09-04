using Newtonsoft.Json;

namespace Fuzn.TestFuzn.Plugins.Newtonsoft;

public class PluginConfiguration
{
    public JsonSerializerSettings JsonSerializerSettings { get; set; } = new JsonSerializerSettings();
    public int Priority { get; set; }
}
