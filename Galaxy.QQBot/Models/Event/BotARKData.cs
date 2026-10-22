namespace Galaxy.QQBot.Models.Event;

public class BotARKData
{
    public string prompt { get; set; } = string.Empty;
    public string ark_type { get; set; } = string.Empty;
    public string ark_name { get; set; } = string.Empty;
    public object fields { get; set; } = null!;
}