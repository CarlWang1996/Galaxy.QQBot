namespace Galaxy.QQBot.Bot;

public class BotSessionContext
{
    public int? LastS { get; set; }

    public int HeartBeatInterval { get; set; }

    public string? SessionId { get; set; }
}