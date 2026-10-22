namespace Galaxy.QQBot.Models.Event;

public class BotMsgElement
{
    public string msg_idx { get; set; } = string.Empty;
    public BotUser author { get; set; } = null!;
    public int message_type { get; set; } = 0;
    public string content { get; set; } = string.Empty;
    public BotMessageAttachment[] attachments { get; set; } = [];
    public BotARKData? ark_data { get; set; } = null;
    public BotMsgElement[] msg_elements = [];
}