using System.Text.Json;
using FuznLabs.TestFuzn.Contracts.Providers;

namespace FuznLabs.TestFuzn;

public class SystemTextJsonSerializerProvider : ISerializerProvider
{
    private readonly JsonSerializerOptions _options;

    public SystemTextJsonSerializerProvider()
    {
        _options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
    }

    public SystemTextJsonSerializerProvider(SystemTextJsonSerializerProviderOptions options)
    {
        _options = options.JsonSerializerSettings;
        Priority = options.Priority;
    }

    public string Serialize<T>(T obj) where T : class
    {
        return JsonSerializer.Serialize(obj, _options);
    }

    public T Deserialize<T>(string json) where T : class
    {
        return JsonSerializer.Deserialize<T>(json, _options);
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
            if (attr is System.Text.Json.Serialization.JsonConverterAttribute ||
                attr is System.Text.Json.Serialization.JsonNumberHandlingAttribute)
            {
                return true;
            }
        }

        // Check for property-level attributes
        var properties = type.GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);

        foreach (var prop in properties)
        {
            if (prop.IsDefined(typeof(System.Text.Json.Serialization.JsonPropertyNameAttribute), inherit: true) ||
                prop.IsDefined(typeof(System.Text.Json.Serialization.JsonIgnoreAttribute), inherit: true) ||
                prop.IsDefined(typeof(System.Text.Json.Serialization.JsonIncludeAttribute), inherit: true) ||
                prop.IsDefined(typeof(System.Text.Json.Serialization.JsonNumberHandlingAttribute), inherit: true) ||
                prop.IsDefined(typeof(System.Text.Json.Serialization.JsonConverterAttribute), inherit: true))
            {
                return true;
            }
        }

        return false;
    }

    public int Priority { get; }
}
