using System.Text.Json.Serialization;

namespace Galaxy.QQBot.Models.Message;

public class BotMediaInfo
{
    [JsonPropertyName("file_info")]
    public string? FileInfo { get; set; } = null;
}