namespace Galaxy.QQBot.Models.Event;

public class BotMessageAttachment
{
    public string url { get; set; } = string.Empty;
    public string filename { get; set; } = string.Empty;
    public int width { get; set; } = 0;
    public int height { get; set; } = 0;
    public int size { get; set; } = 0;
    public string content_type { get; set; } = string.Empty;
    public string voice_wav_url { get; set; } = string.Empty;
    public string asr_refer_text { get; set; } = string.Empty;
}
