using System.Text.Json.Serialization;

namespace Galaxy.QQBot.Models.Event;

public class BotMessageAttachment
{
    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;

    [JsonPropertyName("filename")]
    public string Filename { get; set; } = string.Empty;

    [JsonPropertyName("width")]
    public int Width { get; set; } = 0;

    [JsonPropertyName("height")]
    public int Height { get; set; } = 0;

    [JsonPropertyName("size")]
    public int Size { get; set; } = 0;

    [JsonPropertyName("content_type")]
    public string ContentType { get; set; } = string.Empty;

    [JsonPropertyName("voice_wav_url")]
    public string VoiceWavUrl { get; set; } = string.Empty;

    [JsonPropertyName("asr_refer_text")]
    public string AsrReferText { get; set; } = string.Empty;
}
