using System.Text.Json.Serialization;

namespace Galaxy.QQBot.Models.Message;

public class BotInputNotify
{
    [JsonPropertyName("input_type")]
    public int? InputType { get; set; } = null;

    [JsonPropertyName("input_second")]
    public int? InputSecond { get; set; } = null;
}
