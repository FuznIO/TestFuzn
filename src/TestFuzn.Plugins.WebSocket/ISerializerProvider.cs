namespace Fuzn.TestFuzn.Plugins.WebSocket;

/// <summary>
/// Provides JSON serialization and deserialization capabilities for WebSocket messages.
/// </summary>
public interface ISerializerProvider
{
    /// <summary>
    /// Serializes an object to a JSON string.
    /// </summary>
    /// <typeparam name="T">The type of object to serialize.</typeparam>
    /// <param name="obj">The object to serialize.</param>
    /// <returns>A JSON string representation of the object.</returns>
    string Serialize<T>(T obj) where T : class;

    /// <summary>
    /// Deserializes a JSON string to an object of the specified type.
    /// </summary>
    /// <typeparam name="T">The type to deserialize to.</typeparam>
    /// <param name="json">The JSON string to deserialize.</param>
    /// <returns>The deserialized object.</returns>
    T Deserialize<T>(string json) where T : class;
}
