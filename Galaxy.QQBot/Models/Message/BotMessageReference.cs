using System.Text.Json.Serialization;

namespace Galaxy.QQBot.Models.Message;

public class BotMessageReference
{
    [JsonPropertyName("message_id")]
    public string? MessageId { get; set; } = null;
}
