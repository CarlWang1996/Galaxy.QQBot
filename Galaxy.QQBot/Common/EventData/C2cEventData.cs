namespace Galaxy.QQBot.Common.EventData;

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

public class BotMessageScene
{
    public string source { get; set; } = string.Empty;
    public string[] ext { get; set; } = [];
}

public class BotMessageAttachment
{
    public string url { get; set; } = string.Empty;
    public string filename { get; set; } = string.Empty;
    public int width { get; set; } = 0;
    public int height { get; set; } = 0;
    public int size { get; set; } = 0;
    public string content_type { get; set; } = string.Empty;
    public string voice_wav_url { get; set; } = string.Empty;
    public string asr_refer_text { get; set; } = string.Empty;
}

public class BotARKData
{
    public string prompt { get; set; } = string.Empty;
    public string ark_type { get; set; } = string.Empty;
    public string ark_name { get; set; } = string.Empty;
    public object fields { get; set; } = null!;
}

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
