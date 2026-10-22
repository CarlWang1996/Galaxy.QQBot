namespace Galaxy.QQBot.Common;

public class BotMessageMarkdown
{
    public string? content { get; set; } = null;
    public bool? force_verify_image_resource { get; set; } = null;
}

public class BotKeyBoard
{
    public BotKeyBoardContent? content { get; set; } = null;
}

public class BotKeyBoardContent
{
    public BotKeyBoardRow[]? rows { get; set; } = null;
}
public class BotKeyBoardRow
{
    public BotKeyBoardButton[]? buttons { get; set; } = null;
}

public class BotKeyBoardButton
{
    public string? id { get; set; } = null;
    public BotKeyBoardRenderData? render_data { get; set; } = null;
    public BotKeyBoardAction? action { get; set; } = null;
    public string? group_id { get; set; } = null;
}

public class BotKeyBoardRenderData
{
    public string? label { get; set; } = null;
    public string? visited_label { get; set; } = null;
    public int? style { get; set; } = null;
}

public class BotKeyBoardAction
{
    public int? type { get; set; } = null;
    public BotKeyBoardPermission? permission { get; set; } = null;
    public string? data { get; set; } = null;
    public string? unsupport_tips { get; set; } = null;
    public bool? enter { get; set; } = null;
    public bool? reply { get; set; } = null;
    public int? anchor { get; set; } = null;
    public BotKeyBoardModal? modal { get; set; } = null;
}

public class BotKeyBoardPermission
{
    public int? type { get; set; } = null;
    public string[]? specify_user_ids { get; set; } = null;
}

public class BotKeyBoardModal
{
    public string? content { get; set; } = null;
    public string? confirm_text { get; set; } = null;
    public string? cancel_text { get; set; } = null;
}

public class BotMediaInfo
{
    public string? file_info { get; set; } = null;
}

public class BotMessageReference
{
    public string? message_id { get; set; } = null;
}

public class BotInputNotify
{
    public int? input_type { get; set; } = null;
    public int? input_second { get; set; } = null;
}

public class SendBotMessageRequest
{
    public int msg_type { get; set; } = 0;
    public string? content { get; set; } = null;
    public BotMessageMarkdown? markdown { get; set; } = null;
    public BotKeyBoard? keyboard { get; set; } = null;
    public string? msg_id { get; set; } = null;
    public string? event_id { get; set; } = null;
    public int? msg_seq { get; set; } = 0;
    public BotMediaInfo? media { get; set; } = null;
    public BotMessageReference? message_reference { get; set; } = null;
    public bool? is_wakeup { get; set; } = false;
    public BotInputNotify? input_notify { get; set; } = null;
}

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

public class BotInteractionData
{
    public int type { get; set; } = 0;
    public BotInteractionResolved resolved { get; set; } = null!;
}

public class BotInteractionResolved
{
    public string button_data { get; set; } = string.Empty;
    public string button_id { get; set; } = string.Empty;
    public string feature_id { get; set; } = string.Empty;
    public string message_id { get; set; } = string.Empty;
    public string feedback_opt { get; set; } = string.Empty;
    public int? @checked { get; set; } = null;
    public string action { get; set; } = string.Empty;
    public BotInteractionMessageScene? message_scene = null;
    public BotInteractionAuthorizeData? authorize_data = null;

}

public class BotInteractionMessageScene
{
    public string[] ext { get; set; } = [];
}

public class BotInteractionAuthorizeData
{
    public string opt_scene { get; set; } = string.Empty;
    public string scope { get; set; } = string.Empty;
}

public class BotInfo
{
    public string id { get; set; } = string.Empty;
    public string username { get; set; } = string.Empty;
    public bool bot { get; set; } = false;
}
