using System.Text.Json.Serialization;

namespace Galaxy.QQBot.Models.Upload;

public class BotUploadRequest
{
    [JsonPropertyName("file_type")]
    public int? FileType { get; set; } = null;

    [JsonPropertyName("url")]
    public string? Url { get; set; } = null;

    [JsonPropertyName("srv_send_msg")]
    public bool? SrvSendMsg { get; set; } = null;

    [JsonPropertyName("upload_id")]
    public string? UploadId { get; set; } = null;

    [JsonPropertyName("file_name")]
    public string? FileName { get; set; } = null;

    [JsonPropertyName("file_data")]
    public string? FileData { get; set; } = null;
}
