using System.Text.Json.Serialization;

namespace Galaxy.QQBot.Common;

public class Payload
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("op")]
    public int Op { get; set; } = 0;

    [JsonPropertyName("d")]
    public object D { get; set; } = default!;

    [JsonPropertyName("s")]
    public int S { get; set; } = 0;

    [JsonPropertyName("t")]
    public string T { get; set; } = string.Empty;

}
