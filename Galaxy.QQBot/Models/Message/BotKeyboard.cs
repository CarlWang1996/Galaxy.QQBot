namespace Galaxy.QQBot.Models.Message;

public class BotKeyboard
{
    public BotKeyboardContent? content { get; set; } = null;
}

public class BotKeyboardContent
{
    public BotKeyboardRow[]? rows { get; set; } = null;
}
public class BotKeyboardRow
{
    public BotKeyboardButton[]? buttons { get; set; } = null;
}

public class BotKeyboardButton
{
    public string? id { get; set; } = null;
    public BotKeyboardRenderData? render_data { get; set; } = null;
    public BotKeyboardAction? action { get; set; } = null;
    public string? group_id { get; set; } = null;
}

public class BotKeyboardRenderData
{
    public string? label { get; set; } = null;
    public string? visited_label { get; set; } = null;
    public int? style { get; set; } = null;
}

public class BotKeyboardAction
{
    public int? type { get; set; } = null;
    public BotKeyboardPermission? permission { get; set; } = null;
    public string? data { get; set; } = null;
    public string? unsupport_tips { get; set; } = null;
    public bool? enter { get; set; } = null;
    public bool? reply { get; set; } = null;
    public int? anchor { get; set; } = null;
    public BotKeyboardModal? modal { get; set; } = null;
}

public class BotKeyboardPermission
{
    public int? type { get; set; } = null;
    public string[]? specify_user_ids { get; set; } = null;
}

public class BotKeyboardModal
{
    public string? content { get; set; } = null;
    public string? confirm_text { get; set; } = null;
    public string? cancel_text { get; set; } = null;
}
