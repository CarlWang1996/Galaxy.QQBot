using Galaxy.QQBot.Common;
using Galaxy.QQBot.Models.Message;
using Galaxy.QQBot.Models.Upload;
using Galaxy.QQBot.Tool;

namespace Galaxy.QQBot.Manager;

public class BotMessageManager
{
    private readonly HttpHelper _httpHelper;
    private readonly AccessTokenManager _accessTokenManager;

    public BotMessageManager(HttpHelper httpHelper, AccessTokenManager accessTokenManager)
    {
        _httpHelper = httpHelper;
        _accessTokenManager = accessTokenManager;
    }

    public Task SendC2cMessageAsync(
        string userId,
        string? content = null,
        string? messageId = null,
        string? eventId = null,
        int? sequence = null,
        BotFileInfo? fileinfo = null,
        BotMarkdown? markdown = null,
        BotKeyboard? keyboard = null)
    {
        return SendMessageAsync("user", userId, content, messageId, eventId, sequence, fileinfo, markdown, keyboard);
    }

    public Task SendGroupMessageAsync(
        string groupId,
        string? content = null,
        string? messageId = null,
        string? eventId = null,
        int? sequence = null,
        BotFileInfo? fileinfo = null,
        BotMarkdown? markdown = null,
        BotKeyboard? keyboard = null)
    {
        return SendMessageAsync("group", groupId, content, messageId, eventId, sequence, fileinfo, markdown, keyboard);
    }

    private async Task SendMessageAsync(
        string type,
        string id,
        string? content,
        string? messageId,
        string? eventId,
        int? sequence,
        BotFileInfo? fileinfo,
        BotMarkdown? markdown,
        BotKeyboard? keyboard)
    {
        string urlType = type == "user" ? "users" : "groups";
        string url = $"{Constants.BaseUrl}v2/{urlType}/{id}/messages";

        var request = new SendBotMessageRequest
        {
            MsgId = messageId,
            EventId = !string.IsNullOrEmpty(eventId) ? $"INTERACTION_CREATE:{eventId}" : null,
            MsgSeq = sequence
        };

        if (fileinfo != null)
        {
            string info = await UploadFileAsync(type, id, fileinfo.FilePath, fileinfo.FileType,fileinfo.SrvSendMessage);
            request.MsgType = 7;
            request.Media = new BotMediaInfo { FileInfo = info };
        }
        else if (markdown != null)
        {
            request.MsgType = 2;
            request.Markdown = markdown;
            request.Keyboard = keyboard;
        }
        else if (!string.IsNullOrEmpty(content))
        {
            request.MsgType = 0;
            request.Content = content;
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
            FileName = Path.GetFileName(filePath),
            FileSize = fileInfo.Length.ToString(),
            FileType = fileType,
            Md5 = await FileMd5Helper.GetFullMd5Async(filePath),
            Sha1 = await FileHashHelper.GetFileSha1Async(filePath),
            Md510m = await FileMd5Helper.GetPartialMd5Async(filePath)
        };

        string prepareJson = await _httpHelper.PostAsync($"{baseUrl}/upload_prepare", _accessTokenManager.AccessToken, prepareReq);
        var prepareResp = JsonHelper.DeserializeElement<BotUploadPrepareResponse>(prepareJson)!;
        var parts = prepareResp.Parts.OrderBy(s => s.Index).ToList();
        if (parts.Count == 0)
        {
            throw new InvalidOperationException("upload_prepare 未返回任何分块信息。");
        }

        long declaredTotal = 0;
        foreach (var p in parts)
        {
            declaredTotal += long.Parse(p.BlockSize);
        }

        if (declaredTotal != fileInfo.Length)
        {
            throw new InvalidOperationException($"分块总大小({declaredTotal})与文件大小({fileInfo.Length})不一致，终止上传。");
        }

        await using FileStream stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 64 * 1024, useAsync: true);
        using var md5 = System.Security.Cryptography.MD5.Create();
        long uploadedTotal = 0;

        for (int i = 0; i < parts.Count; i++)
        {
            var block = parts[i];
            int expectedSize = int.Parse(block.BlockSize);
            bool isLastBlock = i == parts.Count - 1;
            byte[] blockBuffer = new byte[expectedSize];
            int n = await ReadFullyAsync(stream, blockBuffer, 0, expectedSize);

            if (n != expectedSize)
            {
                throw new InvalidOperationException($"分块 {block.Index} 读取不完整：期望 {expectedSize} 字节，实际 {n} 字节。");
            }

            await _httpHelper.PutAsyncWithoutToken(block.PresignedUrl, blockBuffer);
            string blockMd5 = Convert.ToHexString(md5.ComputeHash(blockBuffer)).ToLowerInvariant();

            await _httpHelper.PostAsync($"{baseUrl}/upload_part_finish",_accessTokenManager.AccessToken, new
                {
                    upload_id = prepareResp.UploadId,
                    part_index = block.Index,
                    block_size = n.ToString(),
                    md5 = blockMd5
                });

            uploadedTotal += n;
        }

        if (uploadedTotal != fileInfo.Length)
        {
            throw new InvalidOperationException($"上传总大小({uploadedTotal})与文件大小({fileInfo.Length})不一致，终止合并。");
        }

        var mergeReq = new
        {
            file_type = fileType,
            srv_send_msg = sendMsg,
            upload_id = prepareResp.UploadId,
            file_name = Path.GetFileName(filePath)
        };

        string mergeJson = await _httpHelper.PostAsync($"{baseUrl}/files", _accessTokenManager.AccessToken, mergeReq);
        var mergeResp = JsonHelper.DeserializeElement<BotUploadResponse>(mergeJson);
        return mergeResp?.FileInfo!;
    }

    private static async Task<int> ReadFullyAsync(Stream stream, byte[] buffer, int offset, int count)
    {
        int totalRead = 0;
        while (totalRead < count)
        {
            int read = await stream.ReadAsync(buffer.AsMemory(offset + totalRead, count - totalRead));
            if (read == 0)
            {
                break;
            }

            totalRead += read;
        }

        return totalRead;
    }
}