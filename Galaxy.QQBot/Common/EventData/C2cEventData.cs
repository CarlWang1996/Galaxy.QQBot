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

public class BotUploadPrepareRequest
{
    public int file_type { get; set; } = 0;
    public string file_size { get; set; } = string.Empty;
    public string file_name { get; set; } = string.Empty;
    public string md5 { get; set; } = string.Empty;
    public string sha1 { get; set; } = string.Empty;
    public string md5_10m { get; set; } = string.Empty;
}

public class BotUploadPrepareResponse
{
    public string upload_id { get; set; } = string.Empty;
    public string block_size { get; set; } = string.Empty;
    public BotUploadPart[] parts { get; set; } = [];
    public BotUploadConfig config { get; set; } = null!;
}

public class BotUploadPart
{
    public int index { get; set; } = 0;
    public string presigned_url { get; set; } = string.Empty;
    public string block_size { get; set; } = string.Empty;
}

public class BotUploadConfig
{
    public int concurrency { get; set; } = 0;
    public int retry_timeout { get; set; } = 0;
    public int retry_delay { get; set; } = 0;
}

public class BotUploadRequest
{
    public int? file_type { get; set; } = null;
    public string? url { get; set; } = null;
    public bool? srv_send_msg { get; set; } = null;
    public string? upload_id { get; set; } = null;
    public string? file_name { get; set; } = null;
    public string? file_data { get; set; } = null;
}

public class BotUploadResponse
{
    public string file_uuid { get; set; } = string.Empty;
    public string file_info { get; set; } = string.Empty;
    public int ttl { get; set; } = 0;
    public string id { get; set; } = string.Empty;
    public string raw_url { get; set; } = string.Empty;
}