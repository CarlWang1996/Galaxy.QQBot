using System.Text.Json.Serialization;

namespace Galaxy.QQBot.Models.Event;

public record class BotMsgElement
{
    [JsonPropertyName("msg_idx")]
    public string MsgIdx { get; init; } = string.Empty;

    [JsonPropertyName("author")]
    public BotUser Author { get; init; } = null!;

    [JsonPropertyName("message_type")]
    public int MessageType { get; init; } = 0;

    [JsonPropertyName("content")]
    public string Content { get; init; } = string.Empty;

    [JsonPropertyName("attachments")]
    public BotMessageAttachment[] Attachments { get; init; } = [];

    [JsonPropertyName("ark_data")]
    public BotARKData? ArkData { get; init; } = null;

    [JsonPropertyName("msg_elements")]
    public BotMsgElement[] MsgElements { get; init; } = [];
}