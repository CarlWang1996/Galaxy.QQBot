using Galaxy.QQBot.Manager;
using Galaxy.QQBot.Models.Event;
using Galaxy.QQBot.Models.Message;
using System.Text.Json.Serialization;

namespace Galaxy.QQBot.EventData;

public record class InteractionEventData
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("type")]
    public int Type { get; init; } = 0;

    [JsonPropertyName("scene")]
    public string Scene { get; init; } = string.Empty;

    [JsonPropertyName("chat_type")]
    public int ChatType { get; init; } = 0;

    [JsonPropertyName("timestamp")]
    public string Timestamp { get; init; } = string.Empty;

    [JsonPropertyName("user_openid")]
    public string? UserOpenid { get; init; } = null;

    [JsonPropertyName("group_openid")]
    public string? GroupOpenid { get; init; } = null;

    [JsonPropertyName("group_member_openid")]
    public string? GroupMemberOpenid { get; init; } = null;

    [JsonPropertyName("data")]
    public BotInteractionData Data { get; init; } = null!;

    [JsonPropertyName("version")]
    public int Version { get; init; } = 0;

    [JsonPropertyName("application_id")]
    public string ApplicationId { get; init; } = string.Empty;

    [JsonIgnore]
    public BotMessageManager MessageManager { get; internal set; } = null!;

    private int _seq = 0;

    public async Task ReplyAsync(string? content = null, BotMarkdown? markdown = null, BotKeyboard? keyboard = null, BotFileInfo? fileinfo = null, CancellationToken cancellationToken = default)
    {
        if (MessageManager == null)
        {
            throw new InvalidOperationException("MessageManager 尚未初始化，无法直接回复。请确保消息是由 SocketQQBot 接收的。");
        }

        int currentSeq = Interlocked.Increment(ref _seq);
        try
        {
            if (ChatType == 1)
            {
                await MessageManager.SendGroupMessageAsync(
                groupId: GroupOpenid!,
                eventId: Id,
                sequence: currentSeq,
                content: content,
                markdown: markdown,
                keyboard: keyboard,
                fileinfo: fileinfo,
                cancellationToken: cancellationToken);
            }
            else if (ChatType == 2)
            {
                await MessageManager.SendC2cMessageAsync(
                userId: UserOpenid!,
                eventId: Id,
                sequence: currentSeq,
                content: content,
                markdown: markdown,
                keyboard: keyboard,
                fileinfo: fileinfo,
                cancellationToken: cancellationToken
                );
            }
        }
        finally
        {
            await MessageManager.RespondedAsync(Id);
        }
    }
}
