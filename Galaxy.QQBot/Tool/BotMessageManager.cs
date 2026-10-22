using Galaxy.QQBot.Common;
using Galaxy.QQBot.Identity;
using Newtonsoft.Json;

namespace Galaxy.QQBot.Tool;

public interface IBotMessageManager
{
    Task SendC2cMessageAsync(
        string userId, 
        string? content = null, 
        string? messageId = null, 
        string? eventId = null,
        int? sequence = null, 
        BotMessageMarkdown? markdown = null,
        BotKeyBoard? keyboard = null);
    Task SendGroupMessageAsync(
        string groupId,
        string? content = null,
        string? messageId = null,
        string? eventId = null,
        int? sequence = null,
        BotMessageMarkdown? markdown = null,
        BotKeyBoard? keyboard = null);
}

public class BotMessageManager : IBotMessageManager
{
    private readonly IHttpHelper _httpHelper;
    private readonly IAccessTokenManager _accessTokenManager;

    public BotMessageManager(
        IHttpHelper httpHelper,
        IAccessTokenManager accessTokenManager)
    {
        _httpHelper = httpHelper;
        _accessTokenManager = accessTokenManager;
    }

    public async Task SendC2cMessageAsync(
        string userId,
    string? content = null,
    string? messageId = null,
    string? eventId = null,
    int? sequence = null,
    BotMessageMarkdown? markdown = null,
    BotKeyBoard? keyboard = null)
    {
        string url = $"{Constants.BaseUrl}v2/users/{userId}/messages";
        await SendMessageAsync(url, content, messageId, eventId, sequence, markdown, keyboard);
    }

    public async Task SendGroupMessageAsync(
        string groupId,
        string? content = null,
        string? messageId = null,
        string? eventId = null,
        int? sequence = null,
        BotMessageMarkdown? markdown = null,
        BotKeyBoard? keyboard = null)
    {
        string url = $"{Constants.BaseUrl}v2/groups/{groupId}/messages";
        await SendMessageAsync(url, content, messageId, eventId, sequence, markdown, keyboard);
    }

    private async Task SendMessageAsync(
        string url,
        string? content,
        string? messageId,
        string? eventId,
        int? sequence,
        BotMessageMarkdown? markdown,
        BotKeyBoard? keyboard)
    {
        SendBotMessageRequest request;

        if (!string.IsNullOrEmpty(content))
        {
            request = new SendBotMessageRequest
            {
                msg_type = 0,
                content = content,
                msg_id = messageId,
                event_id = $"INTERACTION_CREATE:{eventId}",
                msg_seq = sequence
            };
        }
        else if (markdown != null)
        {
            request = new SendBotMessageRequest
            {
                msg_type = 2,
                markdown = markdown,
                keyboard = keyboard,
                msg_id = messageId,
                event_id = $"INTERACTION_CREATE:{eventId}",
                msg_seq = sequence
            };
        }
        else
        {
            throw new NotSupportedException("消息类型不支持");
        }

        await _httpHelper.PostAsync(url, _accessTokenManager.AccessToken, request);
    }
}
