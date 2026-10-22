using Galaxy.QQBot.Models.Event;
using System.Text.Json.Serialization;

namespace Galaxy.QQBot.EventData;

public record class ReadyEventData
{
    [JsonPropertyName("version")]
    public int Version { get; init; } = 0;

    [JsonPropertyName("session_id")]
    public string SessionId { get; init; } = string.Empty;

    [JsonPropertyName("user")]
    public BotUser User { get; init; } = null!;

    [JsonPropertyName("shard")]
    public int[] Shard { get; init; } = null!;
}
