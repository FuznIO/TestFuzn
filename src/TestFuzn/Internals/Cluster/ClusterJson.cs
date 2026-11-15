using System.Text.Json;

namespace Fuzn.TestFuzn.Internals.Cluster;

internal static class ClusterJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    public static string Serialize<T>(T value) => JsonSerializer.Serialize(value, Options);

    public static T Deserialize<T>(string json)
    {
        var value = JsonSerializer.Deserialize<T>(json, Options);
        if (value == null)
            throw new InvalidDataException($"Expected a {typeof(T).Name} but the JSON was null.");

        return value;
    }
}
