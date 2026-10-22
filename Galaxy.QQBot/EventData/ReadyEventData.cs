using Galaxy.QQBot.Models.Event;

namespace Galaxy.QQBot.EventData;

public class ReadyEventData
{
    public int version { get; set; } = 0;
    public string session_id { get; set; } = string.Empty;
    public BotUser user { get; set; } = null!;
    public int[] shard { get; set; } = null!;
}
