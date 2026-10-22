using System.Text.Json.Serialization;

namespace Galaxy.QQBot.Models.Event;

public class BotMsgElement
{
    [JsonPropertyName("msg_idx")]
    public string MsgIdx { get; set; } = string.Empty;

    [JsonPropertyName("author")]
    public BotUser Author { get; set; } = null!;

    [JsonPropertyName("message_type")]
    public int MessageType { get; set; } = 0;

    [JsonPropertyName("content")]
    public string Content { get; set; } = string.Empty;

    [JsonPropertyName("attachments")]
    public BotMessageAttachment[] Attachments { get; set; } = [];

    [JsonPropertyName("ark_data")]
    public BotARKData? ArkData { get; set; } = null;

    [JsonPropertyName("msg_elements")]
    public BotMsgElement[] MsgElements { get; set; } = [];
}