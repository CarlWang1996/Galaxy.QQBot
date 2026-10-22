using System.Text.Json.Serialization;

namespace Galaxy.QQBot.Models.Upload;

internal class BotUploadPrepareResponse
{
    [JsonPropertyName("upload_id")]
    public string UploadId { get; set; } = string.Empty;

    [JsonPropertyName("block_size")]
    public string BlockSize { get; set; } = string.Empty;

    [JsonPropertyName("parts")]
    public BotUploadPart[] Parts { get; set; } = [];

    [JsonPropertyName("config")]
    public BotUploadConfig Config { get; set; } = null!;
}

internal class BotUploadPart
{
    [JsonPropertyName("index")]
    public int Index { get; set; } = 0;

    [JsonPropertyName("presigned_url")]
    public string PresignedUrl { get; set; } = string.Empty;

    [JsonPropertyName("block_size")]
    public string BlockSize { get; set; } = string.Empty;
}

internal class BotUploadConfig
{

    [JsonPropertyName("concurrency")]
    public int Concurrency { get; set; } = 0;

    [JsonPropertyName("retry_timeout")]
    public int RetryTimeout { get; set; } = 0;

    [JsonPropertyName("retry_delay")]
    public int RetryDelay { get; set; } = 0;
}
