using System.Dynamic;
using System.Text.Json;

namespace TestFusion.HttpTesting.Internals;

internal static class DynamicHelper
{
    public static dynamic ParseJsonToDynamic(string jsonString)
    {
        using JsonDocument doc = JsonDocument.Parse(jsonString);
        JsonElement root = doc.RootElement;

        if (root.ValueKind == JsonValueKind.Array)
        {
            var list = new List<dynamic>();
            foreach (JsonElement element in root.EnumerateArray())
            {
                list.Add(ConvertJsonElementToDynamic(element));
            }
            return list;
        }
        else if (root.ValueKind == JsonValueKind.Object)
        {
            return ConvertJsonElementToDynamic(root);
        }
        else
        {
            throw new InvalidOperationException("JSON must be an object or array.");
        }
    }

    private static dynamic ConvertJsonElementToDynamic(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            var expando = new ExpandoObject();
            var expandoDict = (IDictionary<string, object>) expando;

            foreach (JsonProperty property in element.EnumerateObject())
            {
                expandoDict[property.Name] = ConvertJsonElementToDynamic(property.Value);
            }
            return expando;
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            var list = new List<dynamic>();
            foreach (JsonElement item in element.EnumerateArray())
            {
                list.Add(ConvertJsonElementToDynamic(item));
            }
            return list;
        }
        else
        {
            return element.ValueKind switch
            {
                JsonValueKind.String => element.GetString(),
                JsonValueKind.Number => element.TryGetInt32(out int i) ? i : element.GetDouble(),
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.Null => null,
                _ => throw new InvalidOperationException($"Unsupported JSON value kind: {element.ValueKind}")
            };
        }
    }
}
