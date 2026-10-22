namespace Galaxy.QQBot.Models.Message;

public class SendBotMessageRequest
{
    public int msg_type { get; set; } = 0;
    public string? content { get; set; } = null;
    public BotMarkdown? markdown { get; set; } = null;
    public BotKeyboard? keyboard { get; set; } = null;
    public string? msg_id { get; set; } = null;
    public string? event_id { get; set; } = null;
    public int? msg_seq { get; set; } = 0;
    public BotMediaInfo? media { get; set; } = null;
    public BotMessageReference? message_reference { get; set; } = null;
    public bool? is_wakeup { get; set; } = false;
    public BotInputNotify? input_notify { get; set; } = null;
}
