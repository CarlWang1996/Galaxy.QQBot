using System.Text.Json.Serialization;

namespace Galaxy.QQBot.Models.Message;

public class BotMarkdown
{
    [JsonPropertyName("content")]
    public string? Content { get; set; } = null;

    [JsonPropertyName("force_verify_image_resource")]
    public bool? ForceVerifyImageResource { get; set; } = null;
}