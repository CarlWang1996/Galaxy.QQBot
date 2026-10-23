namespace Galaxy.QQBot.Bot;

internal class BotSessionContext
{
    private int _lastS = -1;

    public int? LastS
    {
        get
        {
            int value = Volatile.Read(ref _lastS);
            return value < 0 ? null : value;
        }
        set => Volatile.Write(ref _lastS, value ?? -1);
    }

    public int HeartBeatInterval { get; set; }

    public string? SessionId { get; set; }
}