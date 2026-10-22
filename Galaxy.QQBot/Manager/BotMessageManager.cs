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
        string? filePath = null,
        BotMarkdown? markdown = null,
        BotKeyboard? keyboard = null)
    {
        await SendMessageAsync("c2c", userId, content, messageId, eventId, sequence, filePath, markdown, keyboard);
    }

    public async Task SendGroupMessageAsync(
        string groupId,
        string? content = null,
        string? messageId = null,
        string? eventId = null,
        int? sequence = null,
        string? filePath = null,
        BotMarkdown? markdown = null,
        BotKeyboard? keyboard = null)
    {
        
        await SendMessageAsync("group", groupId, content, messageId, eventId, sequence, filePath, markdown, keyboard);
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
        string urlType = type == "c2c" ? "user2" : "groups";
        string url = $"{Constants.BaseUrl}v2/{urlType}/{id}/messages";
        SendBotMessageRequest request;

        if (!string.IsNullOrEmpty(content))
        {
            request = new SendBotMessageRequest
            {
                msg_type = 0,
                content = content,
                msg_id = messageId,
                event_id = eventId ?? $"INTERACTION_CREATE:{eventId}",
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
                event_id = eventId ?? $"INTERACTION_CREATE:{eventId}",
                msg_seq = sequence
            };
        }
        else if (!string.IsNullOrEmpty(filePath))
        {

            string fileinfo;
            if(type == "c2c")
            {
                fileinfo = await UploadC2cFile(id, filePath, 4);
            }
            else
            {
                fileinfo = await UploadGroupFile(id, filePath, 4);
            }
            request = new SendBotMessageRequest
            {
                msg_type = 7,
                msg_id = messageId,
                event_id = eventId ?? $"INTERACTION_CREATE:{eventId}",
                msg_seq = sequence,
                media = new BotMediaInfo() { file_info = fileinfo }
            };
        }
        else
        {
            throw new NotSupportedException("消息类型不支持");
        }
        await _httpHelper.PostAsync(url, _accessTokenManager.AccessToken, request);
    }
    public async Task ResponedAsync(string eventId)
    {
        string url = $"{Constants.BaseUrl}interactions/{eventId}";
        await _httpHelper.PutAsync(url, _accessTokenManager.AccessToken, new { code = 0 });
    }

    private async Task<string> UploadC2cFile(string userId, string filePath, int fileType, bool sendMsg = false)
    {
        string url = $"{Constants.BaseUrl}v2/users/{userId}/upload_prepare";
        var fileInfo = new FileInfo(filePath);

        // 1. 准备上传
        BotUploadPrepareRequest request = new BotUploadPrepareRequest
        {
            file_name = Path.GetFileName(filePath),
            file_size = fileInfo.Length.ToString(),
            file_type = fileType,
            md5 = await FileMd5Helper.GetFullMd5Async(filePath),
            sha1 = await FileHashHelper.GetFileSha1Async(filePath),
            md5_10m = await FileMd5Helper.GetPartialMd5Async(filePath)
        };
        string json = await _httpHelper.PostAsync(url, _accessTokenManager.AccessToken, request);
        var prepareResponse = JsonConvert.DeserializeObject<BotUploadPrepareResponse>(json)!;

        // 2. 逐片上传
        using Stream stream = File.OpenRead(filePath);
        foreach (var block in prepareResponse.parts.OrderBy(s => s.index))
        {
            byte[] blockBuffer = new byte[int.Parse(block.block_size)];
            int n = await stream.ReadAsync(blockBuffer.AsMemory(0, blockBuffer.Length));
            if (n == 0) break; // 读完了

            // 【注意】这里组装实际读取的数据，避免包含空余字节
            byte[] exactData = new byte[n];
            Array.Copy(blockBuffer, exactData, n);

            // 2.1 PUT 上传到预签名 URL（通常只需传二进制数据，不带 Token）
            await _httpHelper.PutAsyncWithoutToken(block.presigned_url, exactData);

            // 2.2 计算该分片的 MD5
            string blockMd5;
            using (var md5 = System.Security.Cryptography.MD5.Create())
            {
                byte[] hashBytes = md5.ComputeHash(exactData);
                blockMd5 = Convert.ToHexString(hashBytes).ToLowerInvariant();
            }

            // 2.3 通知服务端分片完成（必须 await！）
            await _httpHelper.PostAsync(
                $"{Constants.BaseUrl}v2/users/{userId}/upload_part_finish",
                _accessTokenManager.AccessToken,
                new
                {
                    upload_id = prepareResponse.upload_id,
                    part_index = block.index,
                    block_size = block.block_size, // 注意：官方字段名就叫 block_size
                    md5 = blockMd5
                });
        }

        // 3. 所有分片上传完成后，发起合并请求（纯净的分片合并参数）
        var mergeRequest = new
        {
            file_type = fileType,
            srv_send_msg = sendMsg,
            upload_id = prepareResponse.upload_id,
            file_name = Path.GetFileName(filePath)
        };

        string mergeJson = await _httpHelper.PostAsync(
            $"{Constants.BaseUrl}v2/users/{userId}/files",
            _accessTokenManager.AccessToken,
            mergeRequest);

        // 4. 解析合并响应（获取 file_info）
        var mergeResponse = JsonConvert.DeserializeObject<BotUploadResponse>(mergeJson);
        return mergeResponse?.file_info!;
    }

    private async Task<string> UploadGroupFile(string groupId, string filePath, int fileType, bool sendMsg = false)
    {
        string url = $"{Constants.BaseUrl}v2/groups/{groupId}/upload_prepare";
        var fileInfo = new FileInfo(filePath);

        // 1. 准备上传
        BotUploadPrepareRequest request = new BotUploadPrepareRequest
        {
            file_name = Path.GetFileName(filePath),
            file_size = fileInfo.Length.ToString(),
            file_type = fileType,
            md5 = await FileMd5Helper.GetFullMd5Async(filePath),
            sha1 = await FileHashHelper.GetFileSha1Async(filePath),
            md5_10m = await FileMd5Helper.GetPartialMd5Async(filePath)
        };
        string json = await _httpHelper.PostAsync(url, _accessTokenManager.AccessToken, request);
        var prepareResponse = JsonConvert.DeserializeObject<BotUploadPrepareResponse>(json)!;

        // 2. 逐片上传
        using Stream stream = File.OpenRead(filePath);
        foreach (var block in prepareResponse.parts.OrderBy(s => s.index))
        {
            byte[] blockBuffer = new byte[int.Parse(block.block_size)];
            int n = await stream.ReadAsync(blockBuffer.AsMemory(0, blockBuffer.Length));
            if (n == 0) break; // 读完了

            // 【注意】这里组装实际读取的数据，避免包含空余字节
            byte[] exactData = new byte[n];
            Array.Copy(blockBuffer, exactData, n);

            // 2.1 PUT 上传到预签名 URL（通常只需传二进制数据，不带 Token）
            await _httpHelper.PutAsyncWithoutToken(block.presigned_url, exactData);

            // 2.2 计算该分片的 MD5
            string blockMd5;
            using (var md5 = System.Security.Cryptography.MD5.Create())
            {
                byte[] hashBytes = md5.ComputeHash(exactData);
                blockMd5 = Convert.ToHexString(hashBytes).ToLowerInvariant();
            }

            // 2.3 通知服务端分片完成（必须 await！）
            await _httpHelper.PostAsync(
                $"{Constants.BaseUrl}v2/groups/{groupId}/upload_part_finish",
                _accessTokenManager.AccessToken,
                new
                {
                    upload_id = prepareResponse.upload_id,
                    part_index = block.index,
                    block_size = block.block_size, // 注意：官方字段名就叫 block_size
                    md5 = blockMd5
                });
        }

        // 3. 所有分片上传完成后，发起合并请求（纯净的分片合并参数）
        var mergeRequest = new
        {
            file_type = fileType,
            srv_send_msg = sendMsg,
            upload_id = prepareResponse.upload_id,
            file_name = Path.GetFileName(filePath)
        };

        string mergeJson = await _httpHelper.PostAsync(
            $"{Constants.BaseUrl}v2/groups/{groupId}/files",
            _accessTokenManager.AccessToken,
            mergeRequest);

        // 4. 解析合并响应（获取 file_info）
        var mergeResponse = JsonConvert.DeserializeObject<BotUploadResponse>(mergeJson);
        return mergeResponse?.file_info!;
    }
}
