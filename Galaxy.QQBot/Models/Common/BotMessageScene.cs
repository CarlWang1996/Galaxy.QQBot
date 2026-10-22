using System.Text.Json.Serialization;

namespace Galaxy.QQBot.Models.Common;

public record class BotMessageScene
{
    [JsonPropertyName("source")]
    public string Source { get; init; } = string.Empty;

    [JsonPropertyName("ext")]
    public string[] Ext { get; init; } = [];
}
