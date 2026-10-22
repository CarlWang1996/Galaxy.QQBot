using System.Text.Json.Serialization;

namespace Galaxy.QQBot.Models.Message;

public class SendBotMessageRequest
{
    [JsonPropertyName("msg_type")]
    public int MsgType { get; set; } = 0;

    [JsonPropertyName("content")]
    public string? Content { get; set; } = null;

    [JsonPropertyName("markdown")]
    public BotMarkdown? Markdown { get; set; } = null;

    [JsonPropertyName("keyboard")]
    public BotKeyboard? Keyboard { get; set; } = null;

    [JsonPropertyName("msg_id")]
    public string? MsgId { get; set; } = null;

    [JsonPropertyName("event_id")]
    public string? EventId { get; set; } = null;

    [JsonPropertyName("msg_seq")]
    public int? MsgSeq { get; set; } = 0;

    [JsonPropertyName("media")]
    public BotMediaInfo? Media { get; set; } = null;

    [JsonPropertyName("message_reference")]
    public BotMessageReference? MessageReference { get; set; } = null;

    [JsonPropertyName("is_wakeup")]
    public bool? IsWakeup { get; set; } = false;

    [JsonPropertyName("input_notify")]
    public BotInputNotify? InputNotify { get; set; } = null;
}
