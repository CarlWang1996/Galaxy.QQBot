namespace Galaxy.QQBot.Models.Upload;

public class BotUploadPrepareResponse
{
    public string upload_id { get; set; } = string.Empty;
    public string block_size { get; set; } = string.Empty;
    public BotUploadPart[] parts { get; set; } = [];
    public BotUploadConfig config { get; set; } = null!;
}

public class BotUploadPart
{
    public int index { get; set; } = 0;
    public string presigned_url { get; set; } = string.Empty;
    public string block_size { get; set; } = string.Empty;
}

public class BotUploadConfig
{
    public int concurrency { get; set; } = 0;
    public int retry_timeout { get; set; } = 0;
    public int retry_delay { get; set; } = 0;
}
