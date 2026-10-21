using System;
using System.Collections.Generic;
using System.Text;

namespace Galaxy.QQBot.Common.EventData;

public class GroupEventData
{
    public string id { get; set; } = string.Empty;
    public BotUser author { get; set; } = null!;
    public string content { get; set; } = string.Empty;
    public string group_openid { get; set; } = string.Empty;
    public string timestamp { get; set; } = string.Empty;
    public int message_type { get; set; } = 0;
    public BotMessageScene message_scene { get; set; } = null!;
    public BotMessageAttachment[] attachments = [];
    public BotUser[] mentions = [];
    public BotARKData? ark_data = null;
    public BotMsgElement[] msg_elements = [];
}
