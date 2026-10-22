namespace Galaxy.QQBot.Models.Event;

public class BotUser
{
    public string id { get; set; } = string.Empty;
    public string username { get; set; } = string.Empty;
    public bool bot { get; set; } = false;
    public string? union_openid { get; set; } = null;
    public string? union_user_account { get; set; } = null;
    public string? user_openid { get; set; } = null;
    public string? member_openid { get; set; } = null;
    public string member_role { get; set; } = string.Empty;
}
