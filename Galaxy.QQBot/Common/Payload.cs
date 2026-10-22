namespace Galaxy.QQBot.Common;

public class Payload
{
    public string id { get; set; } = string.Empty;
    public int op { get; set; } = 0;
    public object d { get; set; } = default!;
    public int s { get; set; } = 0;
    public string t { get; set; } = string.Empty;

}
