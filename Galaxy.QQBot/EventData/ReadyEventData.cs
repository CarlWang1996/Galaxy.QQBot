using Galaxy.QQBot.Models.Event;
using System.Text.Json.Serialization;

namespace Galaxy.QQBot.EventData;

public class ReadyEventData
{
    [JsonPropertyName("version")]
    public int Version { get; set; } = 0;

    [JsonPropertyName("session_id")]
    public string SessionId { get; set; } = string.Empty;

    [JsonPropertyName("user")]
    public BotUser User { get; set; } = null!;

    [JsonPropertyName("shard")]
    public int[] Shard { get; set; } = null!;
}
