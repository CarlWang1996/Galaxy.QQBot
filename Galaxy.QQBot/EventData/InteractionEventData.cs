using Galaxy.QQBot.Models.Event;
using System.Text.Json.Serialization;

namespace Galaxy.QQBot.EventData;

public class InteractionEventData
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public int Type { get; set; } = 0;

    [JsonPropertyName("scene")]
    public string Scene { get; set; } = string.Empty;

    [JsonPropertyName("chat_type")]
    public int ChatType { get; set; } = 0;

    [JsonPropertyName("timestamp")]
    public string Timestamp { get; set; } = string.Empty;

    [JsonPropertyName("user_openid")]
    public string? UserOpenid { get; set; } = null;

    [JsonPropertyName("group_openid")]
    public string? GroupOpenid { get; set; } = null;

    [JsonPropertyName("group_member_openid")]
    public string? GroupMemberOpenid { get; set; } = null;

    [JsonPropertyName("data")]
    public BotInteractionData Data { get; set; } = null!;

    [JsonPropertyName("version")]
    public int Version { get; set; } = 0;

    [JsonPropertyName("application_id")]
    public string ApplicationId { get; set; } = string.Empty;
}
