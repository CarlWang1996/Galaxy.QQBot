using Newtonsoft.Json;

namespace Galaxy.QQBot.Tool;

public interface IJsonHelper
{
    string Serialize<T>(T obj);
    T? Deserialize<T>(string json);
    T? DeserializeElement<T>(object element);
}

public class JsonHelper : IJsonHelper
{
    // 统一的序列化设置
    private static readonly JsonSerializerSettings Settings = new()
    {
        DateParseHandling = DateParseHandling.None,
        NullValueHandling = NullValueHandling.Ignore
    };

    public string Serialize<T>(T obj)
    {
        if (obj == null)
        {
            return string.Empty;
        }  
        return JsonConvert.SerializeObject(obj, Settings);
    }

    public T? Deserialize<T>(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return default;
        }
        try
        {
            return JsonConvert.DeserializeObject<T>(json, Settings);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"JSON 反序列化失败，目标类型: {typeof(T).Name}", ex);
        }
    }

    public T? DeserializeElement<T>(object element)
    {
        if (element == null) return default;

        // 如果是 Newtonsoft 的 JToken，可以直接 ToObject
        if (element is Newtonsoft.Json.Linq.JToken jToken)
        {
            return jToken.ToObject<T>();
        }

        // 兜底转字符串再解析
        string json = element.ToString() ?? string.Empty;
        return Deserialize<T>(json);
    }
}

