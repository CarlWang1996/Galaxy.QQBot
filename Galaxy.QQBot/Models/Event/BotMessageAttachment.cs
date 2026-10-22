using System.Text.Json.Serialization;

namespace Galaxy.QQBot.Models.Event;

public record class BotMessageAttachment
{
    [JsonPropertyName("url")]
    public string Url { get; init; } = string.Empty;

    [JsonPropertyName("filename")]
    public string Filename { get; init; } = string.Empty;

    [JsonPropertyName("width")]
    public int Width { get; init; } = 0;

    [JsonPropertyName("height")]
    public int Height { get; init; } = 0;

    [JsonPropertyName("size")]
    public int Size { get; init; } = 0;

    [JsonPropertyName("content_type")]
    public string ContentType { get; init; } = string.Empty;

    [JsonPropertyName("voice_wav_url")]
    public string VoiceWavUrl { get; init; } = string.Empty;

    [JsonPropertyName("asr_refer_text")]
    public string AsrReferText { get; init; } = string.Empty;
}
