using Galaxy.QQBot.Manager;
using Galaxy.QQBot.Tool;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Galaxy.QQBot.Bot;

public static class QQBotFactory
{
    public static SocketQQBot Create(string appId, string appSecret, int shards = 1,ILoggerFactory? loggerFactory = null)
    {
        loggerFactory ??= NullLoggerFactory.Instance;
        var context = new BotSessionContext();
        var options = Options.Create(new QQBotOptions { AppId = appId, AppSecret = appSecret, Shards = shards });
        var httpHelper = new HttpHelper(new HttpClient());
        var webSocket = new WebSocketClient();
        var tokenManager = new AccessTokenManager(httpHelper, loggerFactory.CreateLogger<AccessTokenManager>());
        var botMessageManager = new BotMessageManager(httpHelper, tokenManager);
        var heartbeatManager = new HeartbeatManager(webSocket, context, loggerFactory.CreateLogger<HeartbeatManager>());
        return new SocketQQBot(webSocket, httpHelper, tokenManager, loggerFactory.CreateLogger<SocketQQBot>(), options, botMessageManager, context, heartbeatManager);
    }
}
