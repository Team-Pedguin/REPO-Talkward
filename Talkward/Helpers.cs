using System.Text.Json;

public static class Helpers
{
    public static T Get<T>(this JsonElement element, string name, T defaultValue = default!)
    {
        if (element.TryGetProperty(name, out var property))
        {
            return property.Deserialize<T>() ?? defaultValue;
        }

        return defaultValue;
    }
}