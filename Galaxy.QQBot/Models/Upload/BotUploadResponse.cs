namespace Galaxy.QQBot.Models.Upload;

public class BotUploadResponse
{
    public string file_uuid { get; set; } = string.Empty;
    public string file_info { get; set; } = string.Empty;
    public int ttl { get; set; } = 0;
    public string id { get; set; } = string.Empty;
    public string raw_url { get; set; } = string.Empty;
}
