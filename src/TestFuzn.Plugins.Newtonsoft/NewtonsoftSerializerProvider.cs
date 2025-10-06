using Newtonsoft.Json;
using Fuzn.TestFuzn.Contracts.Providers;

namespace Fuzn.TestFuzn.Plugins.Newtonsoft;

public class NewtonsoftSerializerProvider : ISerializerProvider
{
    private readonly JsonSerializerSettings _jsonSerializerSettings;

    public NewtonsoftSerializerProvider()
    {
        _jsonSerializerSettings = new JsonSerializerSettings();
    }

    public NewtonsoftSerializerProvider(JsonSerializerSettings jsonSerializerSettings)
    {
        _jsonSerializerSettings = jsonSerializerSettings;
    }

    public string Serialize<T>(T obj) where T : class
    {
        if (obj == null)
            throw new ArgumentNullException(nameof(obj));

        var json = JsonConvert.SerializeObject(obj, _jsonSerializerSettings);
        return json;
    }

    public T Deserialize<T>(string json) where T : class
    {
        if (json == null)
            throw new ArgumentNullException(nameof(json));

        var obj = JsonConvert.DeserializeObject<T>(json, _jsonSerializerSettings);
        if (obj == null)
            throw new Exception($"Could not deserialize {typeof(T).Name} from JSON: {json}");
        return obj;
    }
}
