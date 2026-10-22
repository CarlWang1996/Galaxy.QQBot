using Galaxy.QQBot.Common;
using Galaxy.QQBot.Models.Message;
using Galaxy.QQBot.Models.Upload;
using Galaxy.QQBot.Tool;
using Newtonsoft.Json;

namespace Galaxy.QQBot.Manager;

public interface IBotMessageManager
{
    Task SendC2cMessageAsync(
        string userId,
        string? content = null,
        string? messageId = null,
        string? eventId = null,
        int? sequence = null,
        string? filePath = null,
        BotMarkdown? markdown = null,
        BotKeyboard? keyboard = null);

    Task SendGroupMessageAsync(
        string groupId,
        string? content = null,
        string? messageId = null,
        string? eventId = null,
        int? sequence = null,
        string? filePath = null,
        BotMarkdown? markdown = null,
        BotKeyboard? keyboard = null);

    Task ResponedAsync(string eventId);
}

public class BotMessageManager : IBotMessageManager
{
    private readonly IHttpHelper _httpHelper;
    private readonly IAccessTokenManager _accessTokenManager;
    private readonly IJsonHelper _jsonHelper;

    public BotMessageManager(IHttpHelper httpHelper, IAccessTokenManager accessTokenManager, IJsonHelper jsonHelper)
    {
        _httpHelper = httpHelper;
        _accessTokenManager = accessTokenManager;
        _jsonHelper = jsonHelper;
    }

    public Task SendC2cMessageAsync(
        string userId,
        string? content = null,
        string? messageId = null,
        string? eventId = null,
        int? sequence = null,
        string? filePath = null,
        BotMarkdown? markdown = null,
        BotKeyboard? keyboard = null)
    {
        return SendMessageAsync("user", userId, content, messageId, eventId, sequence, filePath, markdown, keyboard);
    }

    public Task SendGroupMessageAsync(
        string groupId,
        string? content = null,
        string? messageId = null,
        string? eventId = null,
        int? sequence = null,
        string? filePath = null,
        BotMarkdown? markdown = null,
        BotKeyboard? keyboard = null)
    {
        return SendMessageAsync("group", groupId, content, messageId, eventId, sequence, filePath, markdown, keyboard);
    }

    private async Task SendMessageAsync(
        string type,
        string id,
        string? content,
        string? messageId,
        string? eventId,
        int? sequence,
        string? filePath,
        BotMarkdown? markdown,
        BotKeyboard? keyboard)
    {
        string urlType = type == "user" ? "users" : "groups";
        string url = $"{Constants.BaseUrl}v2/{urlType}/{id}/messages";

        var request = new SendBotMessageRequest
        {
            msg_id = messageId,
            event_id = !string.IsNullOrEmpty(eventId) ? $"INTERACTION_CREATE:{eventId}" : null,
            msg_seq = sequence
        };

        if (!string.IsNullOrEmpty(filePath))
        {
            string fileinfo = await UploadFileAsync(type, id, filePath, fileType: 4);
            request.msg_type = 7;
            request.media = new BotMediaInfo { file_info = fileinfo };
        }
        else if (markdown != null)
        {
            request.msg_type = 2;
            request.markdown = markdown;
            request.keyboard = keyboard;
        }
        else if (!string.IsNullOrEmpty(content))
        {
            request.msg_type = 0;
            request.content = content;
        }
        else
        {
            throw new NotSupportedException("消息内容不能全为空（content / markdown / filePath 必须传一个）");
        }

        await _httpHelper.PostAsync(url, _accessTokenManager.AccessToken, request);
    }

    public async Task ResponedAsync(string eventId)
    {
        string url = $"{Constants.BaseUrl}interactions/{eventId}";

        await _httpHelper.PutAsync(url, _accessTokenManager.AccessToken, new { code = 0 });
    }

    private async Task<string> UploadFileAsync(string type, string targetId, string filePath, int fileType, bool sendMsg = false)
    {
        string urlType = type == "user" ? "users" : "groups";
        string baseUrl = $"{Constants.BaseUrl}v2/{urlType}/{targetId}";
        var fileInfo = new FileInfo(filePath);
        var prepareReq = new BotUploadPrepareRequest
        {
            file_name = Path.GetFileName(filePath),
            file_size = fileInfo.Length.ToString(),
            file_type = fileType,
            md5 = await FileMd5Helper.GetFullMd5Async(filePath),
            sha1 = await FileHashHelper.GetFileSha1Async(filePath),
            md5_10m = await FileMd5Helper.GetPartialMd5Async(filePath)
        };

        string prepareJson = await _httpHelper.PostAsync($"{baseUrl}/upload_prepare", _accessTokenManager.AccessToken, prepareReq);
        var prepareResp = _jsonHelper.DeserializeElement<BotUploadPrepareResponse>(prepareJson)!;
        using Stream stream = File.OpenRead(filePath);
        foreach (var block in prepareResp.parts.OrderBy(s => s.index))
        {
            byte[] blockBuffer = new byte[int.Parse(block.block_size)];
            int n = await stream.ReadAsync(blockBuffer.AsMemory(0, blockBuffer.Length));
            if (n == 0)
            {
                break;
            }

            byte[] exactData = new byte[n];
            Array.Copy(blockBuffer, exactData, n);
            await _httpHelper.PutAsyncWithoutToken(block.presigned_url, exactData);
            using var md5 = System.Security.Cryptography.MD5.Create();
            string blockMd5 = Convert.ToHexString(md5.ComputeHash(exactData)).ToLowerInvariant();
            await _httpHelper.PostAsync(
                $"{baseUrl}/upload_part_finish",
                _accessTokenManager.AccessToken,
                new
                {
                    upload_id = prepareResp.upload_id,
                    part_index = block.index,
                    block_size = n.ToString(),
                    md5 = blockMd5
                });
        }

        var mergeReq = new
        {
            file_type = fileType,
            srv_send_msg = sendMsg,
            upload_id = prepareResp.upload_id,
            file_name = Path.GetFileName(filePath)
        };

        string mergeJson = await _httpHelper.PostAsync($"{baseUrl}/files", _accessTokenManager.AccessToken, mergeReq);
        var mergeResp = _jsonHelper.DeserializeElement<BotUploadResponse>(mergeJson);
        return mergeResp?.file_info!;
    }
}