using System.Text.Json;
using System.Text.Json.Serialization;

namespace Galaxy.QQBot.Tool;

internal static class JsonHelper
{
    internal static readonly JsonSerializerOptions Options = new JsonSerializerOptions
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static string Serialize<T>(T obj)
    {
        if (obj == null)
        {
            return string.Empty;
        }

        return JsonSerializer.Serialize(obj, Options);
    }

    public static T? Deserialize<T>(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return default;
        }

        try
        {
            return JsonSerializer.Deserialize<T>(json);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"JSON 反序列化失败，目标类型: {typeof(T).Name}", ex);
        }
    }

    public static T? DeserializeElement<T>(object element)
    {
        if (element == null)
        {
            return default;
        }

        string json = element.ToString() ?? string.Empty;
        return Deserialize<T>(json);
    }
}

