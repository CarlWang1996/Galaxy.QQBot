namespace Galaxy.QQBot.Models.Upload;

public class BotUploadPrepareRequest
{
    public int file_type { get; set; } = 0;
    public string file_size { get; set; } = string.Empty;
    public string file_name { get; set; } = string.Empty;
    public string md5 { get; set; } = string.Empty;
    public string sha1 { get; set; } = string.Empty;
    public string md5_10m { get; set; } = string.Empty;
}
