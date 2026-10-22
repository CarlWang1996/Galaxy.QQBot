using System.Text.Json.Serialization;

namespace Galaxy.QQBot.Models.Event;

public record class BotARKData
{
    [JsonPropertyName("prompt")]
    public string Prompt { get; init; } = string.Empty;

    [JsonPropertyName("ark_type")]
    public string ArkType { get; init; } = string.Empty;

    [JsonPropertyName("ark_name")]
    public string ArkName { get; init; } = string.Empty;

    [JsonPropertyName("fields")]
    public object Fields { get; init; } = null!;
}