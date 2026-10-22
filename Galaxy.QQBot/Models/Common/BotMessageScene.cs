using System.Text.Json.Serialization;

namespace Galaxy.QQBot.Models.Common;

public class BotMessageScene
{
    [JsonPropertyName("source")]
    public string Source { get; set; } = string.Empty;

    [JsonPropertyName("ext")]
    public string[] Ext { get; set; } = [];
}
