using System.Text.Json.Serialization;

namespace Galaxy.QQBot.Models.Upload;

public class BotUploadPrepareRequest
{
    [JsonPropertyName("file_type")]
    public int FileType { get; set; } = 0;

    [JsonPropertyName("file_size")]
    public string FileSize { get; set; } = string.Empty;

    [JsonPropertyName("file_name")]
    public string FileName { get; set; } = string.Empty;

    [JsonPropertyName("md5")]
    public string Md5 { get; set; } = string.Empty;

    [JsonPropertyName("sha1")]
    public string Sha1 { get; set; } = string.Empty;

    [JsonPropertyName("md5_10m")]
    public string Md510m { get; set; } = string.Empty;
}
