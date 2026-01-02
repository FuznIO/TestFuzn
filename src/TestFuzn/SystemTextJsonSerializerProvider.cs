using System.Text.Json;
using Fuzn.TestFuzn.Contracts.Providers;

namespace Fuzn.TestFuzn;

/// <summary>
/// Default serializer provider using System.Text.Json for JSON serialization and deserialization.
/// </summary>
public class SystemTextJsonSerializerProvider : ISerializerProvider
{
    private readonly JsonSerializerOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="SystemTextJsonSerializerProvider"/> class with default options.
    /// </summary>
    public SystemTextJsonSerializerProvider()
    {
        _options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SystemTextJsonSerializerProvider"/> class with custom options.
    /// </summary>
    /// <param name="jsonSerializerOptions">The JSON serializer options to use.</param>
    public SystemTextJsonSerializerProvider(JsonSerializerOptions jsonSerializerOptions)
    {
        _options = jsonSerializerOptions;
    }

    /// <summary>
    /// Serializes an object to a JSON string.
    /// </summary>
    /// <typeparam name="T">The type of object to serialize.</typeparam>
    /// <param name="obj">The object to serialize.</param>
    /// <returns>A JSON string representation of the object.</returns>
    public string Serialize<T>(T obj) where T : class
    {
        return JsonSerializer.Serialize(obj, _options);
    }

    /// <summary>
    /// Deserializes a JSON string to an object of the specified type.
    /// </summary>
    /// <typeparam name="T">The type to deserialize to.</typeparam>
    /// <param name="json">The JSON string to deserialize.</param>
    /// <returns>The deserialized object.</returns>
    public T Deserialize<T>(string json) where T : class
    {
        return JsonSerializer.Deserialize<T>(json, _options);
    }
}
