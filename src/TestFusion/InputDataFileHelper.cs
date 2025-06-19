using System.Globalization;
using System.Text.Json;

namespace TestFusion;
public static class InputDataFileHelper
{
    private static JsonSerializerOptions _jsonSerializerOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
    public static async Task<List<object>> LoadFromCsv<T>(string path)
    {
        var result = new List<object>();
        if (!File.Exists(path))
            throw new FileNotFoundException($"CSV file not found: {path}");

        using var reader = new StreamReader(path);
        var headerLine = await reader.ReadLineAsync();
        if (headerLine == null)
            return result;
        var headers = headerLine.Split(',');
        var properties = typeof(T).GetProperties();
        var propertyMap = headers.Select(h => properties.FirstOrDefault(p => string.Equals(p.Name, h.Trim(), StringComparison.OrdinalIgnoreCase))).ToArray();

        string? line;
        while ((line = await reader.ReadLineAsync()) != null)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            var values = line.Split(',');
            var obj = Activator.CreateInstance(typeof(T));
            for (var i = 0; i < headers.Length && i < values.Length; i++)
            {
                var prop = propertyMap[i];
                if (prop != null && obj != null)
                {
                    try
                    {
                        object? value = null;
                        var targetType = Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType;
                        var strVal = values[i].Trim();
                        if (string.IsNullOrEmpty(strVal))
                        {
                            value = null;
                        }
                        else if (targetType == typeof(string))
                            value = strVal;
                        else if (targetType == typeof(Guid))
                            value = Guid.Parse(strVal);
                        else if (targetType == typeof(bool))
                            value = bool.Parse(strVal);
                        else if (targetType == typeof(int))
                            value = int.Parse(strVal, CultureInfo.InvariantCulture);
                        else if (targetType == typeof(long))
                            value = long.Parse(strVal, CultureInfo.InvariantCulture);
                        else if (targetType == typeof(float))
                            value = float.Parse(strVal, CultureInfo.InvariantCulture);
                        else if (targetType == typeof(double))
                            value = double.Parse(strVal, CultureInfo.InvariantCulture);
                        else if (targetType == typeof(decimal))
                            value = decimal.Parse(strVal, CultureInfo.InvariantCulture);
                        else if (targetType == typeof(char))
                            value = strVal[0];
                        else if (targetType == typeof(DateTime))
                            value = DateTime.Parse(strVal, CultureInfo.InvariantCulture);
                        else if (targetType == typeof(DateTimeOffset))
                            value = DateTimeOffset.Parse(strVal, CultureInfo.InvariantCulture);
                        else if (targetType == typeof(TimeSpan))
                            value = TimeSpan.Parse(strVal, CultureInfo.InvariantCulture);
                        else if (targetType.IsEnum)
                            value = Enum.Parse(targetType, strVal);
                        else if (targetType.IsArray)
                        {
                            var elemType = targetType.GetElementType();
                            var arrVals = strVal.Split(';');
                            var arr = Array.CreateInstance(elemType!, arrVals.Length);
                            for (var j = 0; j < arrVals.Length; j++)
                            {
                                arr.SetValue(Convert.ChangeType(arrVals[j], elemType!, CultureInfo.InvariantCulture), j);
                            }
                            value = arr;
                        }
                        else if (targetType.IsGenericType && targetType.GetGenericTypeDefinition() == typeof(List<>))
                        {
                            var elemType = targetType.GetGenericArguments()[0];
                            var arrVals = strVal.Split(';');
                            var list = (System.Collections.IList)Activator.CreateInstance(targetType)!;
                            foreach (var v in arrVals)
                                list.Add(Convert.ChangeType(v, elemType, CultureInfo.InvariantCulture));
                            value = list;
                        }
                        else
                            value = JsonSerializer.Deserialize(strVal, targetType, _jsonSerializerOptions);
                        prop.SetValue(obj, value);
                    }
                    catch { /* Ignore conversion errors */ }
                }
            }
            if (obj != null)
                result.Add(obj);
        }
        return result;
    }
    
    public static async Task<List<object>> LoadFromJson<T>(string path)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException($"JSON file not found: {path}");
        var json = await File.ReadAllTextAsync(path);
        var items = JsonSerializer.Deserialize<List<T>>(json, _jsonSerializerOptions);
        return items?.Cast<object>().ToList() ?? [];
    }
}
