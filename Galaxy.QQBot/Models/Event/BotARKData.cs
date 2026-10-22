using System.Text.Json.Serialization;

namespace Galaxy.QQBot.Models.Event;

public class BotARKData
{
    [JsonPropertyName("prompt")]
    public string Prompt { get; set; } = string.Empty;

    [JsonPropertyName("ark_type")]
    public string ArkType { get; set; } = string.Empty;

    [JsonPropertyName("ark_name")]
    public string ArkName { get; set; } = string.Empty;

    [JsonPropertyName("fields")]
    public object Fields { get; set; } = null!;
}