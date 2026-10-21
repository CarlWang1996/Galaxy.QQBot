namespace Galaxy.QQBot.Bot;

public class QQBotOptionsBuilder
{
    public QQBotOptions Options { get; } = new();

    public QQBotOptionsBuilder UseShards(int count)
    {
        Options.Shards = count;
        return this;
    }

    public QQBotOptionsBuilder UseAppCredentials(string appId, string appSecret)
    {
        Options.AppId = appId;
        Options.AppSecret = appSecret;
        return this;
    }
}
