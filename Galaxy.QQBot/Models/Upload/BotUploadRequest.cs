namespace Galaxy.QQBot.Models.Upload;

public class BotUploadRequest
{
    public int? file_type { get; set; } = null;
    public string? url { get; set; } = null;
    public bool? srv_send_msg { get; set; } = null;
    public string? upload_id { get; set; } = null;
    public string? file_name { get; set; } = null;
    public string? file_data { get; set; } = null;
}
