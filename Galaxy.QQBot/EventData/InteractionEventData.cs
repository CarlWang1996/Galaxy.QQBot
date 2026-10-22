using Galaxy.QQBot.Common;

namespace Galaxy.QQBot.EventData;

public class InteractionEventData
{
    public string id { get; set; } = string.Empty;
    public int type { get; set; } = 0;
    public string scene { get; set; } = string.Empty;
    public int chat_type { get; set; } = 0;
    public string timestamp { get; set; } = string.Empty;
    public string? user_openid { get; set; } = null;
    public string? group_openid { get; set; } = null;
    public string? group_member_openid { get; set; } = null;
    public BotInteractionData data { get; set; } = null!;
    public int version { get; set; } = 0;
    public string application_id { get; set; } = string.Empty;
}
