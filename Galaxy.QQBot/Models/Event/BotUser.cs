using System.Text.Json.Serialization;

namespace Galaxy.QQBot.Models.Event;

public record class BotUser
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("username")]
    public string Username { get; init; } = string.Empty;

    [JsonPropertyName("bot")]
    public bool Bot { get; init; } = false;

    [JsonPropertyName("union_openid")]
    public string? UnionOpenid { get; init; } = null;

    [JsonPropertyName("union_user_account")]
    public string? UnionUserAccount { get; init; } = null;

    [JsonPropertyName("user_openid")]
    public string? UserOpenid { get; init; } = null;

    [JsonPropertyName("member_openid")]
    public string? MemberOpenid { get; init; } = null;

    [JsonPropertyName("member_role")]
    public string MemberRole { get; init; } = string.Empty;
}
