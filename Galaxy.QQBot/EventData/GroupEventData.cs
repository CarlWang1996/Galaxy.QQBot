using Galaxy.QQBot.Models.Common;
using Galaxy.QQBot.Models.Event;
using System.Text.Json.Serialization;

namespace Galaxy.QQBot.EventData;

public class GroupEventData
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("author")]
    public BotUser Author { get; set; } = null!;

    [JsonPropertyName("content")]
    public string Content { get; set; } = string.Empty;

    [JsonPropertyName("group_openid")]
    public string GroupOpenid { get; set; } = string.Empty;

    [JsonPropertyName("timestamp")]
    public string Timestamp { get; set; } = string.Empty;

    [JsonPropertyName("message_type")]
    public int MessageType { get; set; } = 0;

    [JsonPropertyName("message_scene")]
    public BotMessageScene MessageScene { get; set; } = null!;

    [JsonPropertyName("attachments")]
    public BotMessageAttachment[] Attachments { get; set; } = [];

    [JsonPropertyName("mentions")]
    public BotUser[] Mentions { get; set; } = [];

    [JsonPropertyName("ark_data")]
    public BotARKData? ArkData { get; set; } = null;

    [JsonPropertyName("msg_elements")]
    public BotMsgElement[] MsgElements { get; set; } = [];
}
