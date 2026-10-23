using Galaxy.QQBot.Manager;
using Galaxy.QQBot.Models.Common;
using Galaxy.QQBot.Models.Event;
using Galaxy.QQBot.Models.Message;
using System.Text.Json.Serialization;

namespace Galaxy.QQBot.EventData;

public record class C2cEventData
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("author")]
    public BotUser Author { get; init; } = null!;

    [JsonPropertyName("content")]
    public string Content { get; init; } = string.Empty;

    [JsonPropertyName("timestamp")]
    public string Timestamp { get; init; } = string.Empty;

    [JsonPropertyName("message_type")]
    public int MessageType { get; init; } = 0;

    [JsonPropertyName("message_scene")]
    public BotMessageScene MessageScene { get; init; } = null!;

    [JsonPropertyName("attachments")]
    public BotMessageAttachment[] Attachments { get; init; } = [];

    [JsonPropertyName("ark_data")]
    public BotARKData? ArkData { get; init; } = null;

    [JsonPropertyName("msg_elements")]
    public BotMsgElement[] MsgElements { get; init; } = [];

    [JsonIgnore]
    public BotMessageManager MessageManager { get; internal set; } = null!;

    private int _seq = 0;

    public async Task ReplyAsync(string? content = null, string? markdown = null, BotKeyboard? keyboard = null,BotFileInfo? fileinfo = null)
    {
        if (MessageManager == null)
        {
            throw new InvalidOperationException("MessageManager 尚未初始化，无法直接回复。请确保消息是由 SocketQQBot 接收的。");
        }

        string? userOpenid = Author?.UserOpenid;
        if (string.IsNullOrEmpty(userOpenid))
        {
            throw new InvalidOperationException("C2C 事件缺少 author.user_openid，无法回复。");
        }

        int currentSeq = Interlocked.Increment(ref _seq);
        await MessageManager.SendC2cMessageAsync(
            userId: userOpenid,
            messageId: Id,
            sequence: currentSeq,
            content: content,
            markdown: string.IsNullOrEmpty(markdown) ? null : new BotMarkdown() { Content = markdown },
            keyboard:keyboard,
            fileinfo:fileinfo
        );
    }
}