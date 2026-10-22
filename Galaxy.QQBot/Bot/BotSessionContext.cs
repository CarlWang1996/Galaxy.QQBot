namespace Galaxy.QQBot.Bot;

internal class BotSessionContext
{
    public int? LastS { get; set; }

    public int HeartBeatInterval { get; set; }

    public string? SessionId { get; set; }
}