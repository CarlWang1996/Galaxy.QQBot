namespace Galaxy.QQBot.Tool;

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
