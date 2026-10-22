using System.Text.Json.Serialization;

namespace Galaxy.QQBot.Models.Event;

public class BotUser
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("username")]
    public string Username { get; set; } = string.Empty;

    [JsonPropertyName("bot")]
    public bool Bot { get; set; } = false;

    [JsonPropertyName("union_openid")]
    public string? UnionOpenid { get; set; } = null;

    [JsonPropertyName("union_user_account")]
    public string? UnionUserAccount { get; set; } = null;

    [JsonPropertyName("user_openid")]
    public string? UserOpenid { get; set; } = null;

    [JsonPropertyName("member_openid")]
    public string? MemberOpenid { get; set; } = null;

    [JsonPropertyName("member_role")]
    public string MemberRole { get; set; } = string.Empty;
}
