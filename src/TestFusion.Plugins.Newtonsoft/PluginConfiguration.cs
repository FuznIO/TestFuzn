using Newtonsoft.Json;

namespace TestFusion.Plugins.Newtonsoft;

public class PluginConfiguration
{
    public JsonSerializerSettings JsonSerializerSettings { get; set; } = new JsonSerializerSettings();
}
