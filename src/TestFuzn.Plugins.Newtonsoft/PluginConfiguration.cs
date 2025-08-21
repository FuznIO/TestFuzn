using Newtonsoft.Json;

namespace FuznLabs.TestFuzn.Plugins.Newtonsoft;

public class PluginConfiguration
{
    public JsonSerializerSettings JsonSerializerSettings { get; set; } = new JsonSerializerSettings();
    public int Priority { get; set; }
}
