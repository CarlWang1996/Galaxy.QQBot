using System.Text.Json.Serialization;

namespace Galaxy.QQBot.Models.Upload;

public class BotUploadResponse
{
    [JsonPropertyName("file_uuid")]
    public string FileUuid { get; set; } = string.Empty;

    [JsonPropertyName("file_info")]
    public string FileInfo { get; set; } = string.Empty;

    [JsonPropertyName("ttl")]
    public int Ttl { get; set; } = 0;

    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("raw_url")]
    public string RawUrl { get; set; } = string.Empty;
}
