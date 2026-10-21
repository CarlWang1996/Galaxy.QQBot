namespace Galaxy.QQBot.Common.EventData;
public class BotInfo
{
    public string id { get; set; } = string.Empty;
    public string username { get; set; } = string.Empty;
    public bool bot { get; set; } = false;
}

public class ReadyEventData
{
    public int version { get; set; } = 0;
    public string session_id { get; set; } = string.Empty;
    public BotUser user { get; set; } = null!;
    public int[] shard { get; set; } = null!;
}
