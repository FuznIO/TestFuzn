namespace TestFusion.Internals.Utils;

internal class PropertyHelper
{
    public static string GetStringFromProperties(object obj)
    {
        if (obj.GetType().IsValueType
            || obj is string)
            return $"{obj.ToString()}";

        var text = "";

        foreach (var property in obj.GetType().GetProperties())
        {
            if (!string.IsNullOrEmpty(text))
                text += ", ";

            var value = property.GetValue(obj);

            text += $"{property.Name}: \"{value.ToString()}\"";
        }

        return text;
    }
}
