using Newtonsoft.Json;
using Fuzn.TestFuzn.Contracts.Providers;

namespace Fuzn.TestFuzn.Plugins.Newtonsoft.Internals;

internal class NewtonsoftSerializerProvider : ISerializerProvider
{
    private readonly JsonSerializerSettings _jsonSerializerSettings;

    public NewtonsoftSerializerProvider(PluginConfiguration configuration)
    {
        _jsonSerializerSettings = configuration.JsonSerializerSettings;
        Priority = configuration.Priority;
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

    public bool IsSerializerSpecific<T>() where T : class
    {
        var type = typeof(T);

        // If T is a collection, get the element type
        if (typeof(System.Collections.IEnumerable).IsAssignableFrom(type) && type != typeof(string))
        {
            if (type.IsArray)
            {
                type = type.GetElementType();
            }
            else if (type.IsGenericType && type.GetGenericArguments().Length == 1)
            {
                type = type.GetGenericArguments()[0];
            }
        }

        if (type == null)
            return false;

        // Check for class-level attributes
        var classAttributes = type.GetCustomAttributes(inherit: true);
        foreach (var attr in classAttributes)
        {
            if (attr is JsonConverterAttribute ||
                attr is JsonObjectAttribute ||
                attr is JsonArrayAttribute ||
                attr is JsonDictionaryAttribute ||
                attr is JsonContainerAttribute)
            {
                return true;
            }
        }

        // Check for property-level attributes
        var properties = type.GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);

        foreach (var prop in properties)
        {
            if (prop.IsDefined(typeof(JsonPropertyAttribute), inherit: true) ||
                prop.IsDefined(typeof(JsonIgnoreAttribute), inherit: true) ||
                prop.IsDefined(typeof(JsonConverterAttribute), inherit: true) ||
                prop.IsDefined(typeof(JsonRequiredAttribute), inherit: true) ||
                prop.IsDefined(typeof(JsonExtensionDataAttribute), inherit: true))
            {
                return true;
            }
        }

        // Check for constructor-level attributes
        var constructors = type.GetConstructors(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        foreach (var ctor in constructors)
        {
            if (ctor.IsDefined(typeof(JsonConstructorAttribute), inherit: true))
            {
                return true;
            }
        }

        return false;
    }

    public int Priority { get; }
}
