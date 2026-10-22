using Galaxy.QQBot.Manager;
using Galaxy.QQBot.Models.Event;
using Galaxy.QQBot.Models.Message;
using System.Text.Json.Serialization;

namespace Galaxy.QQBot.EventData;

public class InteractionEventData
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public int Type { get; set; } = 0;

    [JsonPropertyName("scene")]
    public string Scene { get; set; } = string.Empty;

    [JsonPropertyName("chat_type")]
    public int ChatType { get; set; } = 0;

    [JsonPropertyName("timestamp")]
    public string Timestamp { get; set; } = string.Empty;

    [JsonPropertyName("user_openid")]
    public string? UserOpenid { get; set; } = null;

    [JsonPropertyName("group_openid")]
    public string? GroupOpenid { get; set; } = null;

    [JsonPropertyName("group_member_openid")]
    public string? GroupMemberOpenid { get; set; } = null;

    [JsonPropertyName("data")]
    public BotInteractionData Data { get; set; } = null!;

    [JsonPropertyName("version")]
    public int Version { get; set; } = 0;

    [JsonPropertyName("application_id")]
    public string ApplicationId { get; set; } = string.Empty;

    [JsonIgnore]
    public BotMessageManager MessageManager { get; internal set; } = null!;

    private int _seq = 0;

    public async Task ReplyAsync(string? content = null, BotMarkdown? markdown = null, BotKeyboard? keyboard = null, BotFileInfo? fileinfo = null)
    {
        if (MessageManager == null)
        {
            throw new InvalidOperationException("MessageManager 尚未初始化，无法直接回复。请确保消息是由 SocketQQBot 接收的。");
        }

        if(ChatType == 1)
        {
            await MessageManager.SendGroupMessageAsync(
            groupId: GroupOpenid!,
            eventId: Id,
            sequence: _seq,
            content: content,
            markdown: markdown,
            keyboard: keyboard,
            fileinfo: fileinfo);
        }
        else if(ChatType == 2)
        {
            await MessageManager.SendC2cMessageAsync(
            userId: UserOpenid!,
            eventId: Id,
            sequence: _seq,
            content: content,
            markdown: markdown,
            keyboard: keyboard,
            fileinfo: fileinfo
            );
        }
        await MessageManager.ResponedAsync(Id);
        _seq++;
    }
}
