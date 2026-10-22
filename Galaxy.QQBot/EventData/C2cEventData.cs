using Galaxy.QQBot.Common;

namespace Galaxy.QQBot.EventData;

public class C2cEventData
{
    public string id { get; set; } = string.Empty;
    public BotUser author { get; set; } = null!;
    public string content { get; set; } = string.Empty;
    public string timestamp { get; set; } = string.Empty;
    public int message_type { get; set; } = 0;
    public BotMessageScene message_scene { get; set; } = null!;
    public BotMessageAttachment[] attachments { get; set; } = [];
    public BotARKData? ark_data { get; set; } = null;
    public BotMsgElement[] msg_elements { get; set; } = [];

}