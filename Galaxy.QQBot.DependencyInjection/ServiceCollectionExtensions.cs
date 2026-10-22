using Galaxy.QQBot.Bot;
using Galaxy.QQBot.Manager;
using Galaxy.QQBot.Tool;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Galaxy.QQBot.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddQQBot(this IServiceCollection services, Action<QQBotOptionsBuilder> configure)
    {
        var builder = new QQBotOptionsBuilder();
        configure(builder);
        services.AddSingleton(Options.Create(builder.Options));
        services.AddHttpClient<IHttpHelper, HttpHelper>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(30);
            client.DefaultRequestHeaders.Add("User-Agent", "Galaxy.QQBot/1.0");
            client.DefaultRequestHeaders.Add("Accept", "application/json");
        }).ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
        {
            PooledConnectionLifetime = TimeSpan.FromMinutes(2)
        });
        services.AddSingleton<IJsonHelper, JsonHelper>();
        services.AddSingleton<IWebSocketClient, WebSocketClient>();
        services.AddSingleton<IAccessTokenManager, AccessTokenManager>();
        services.AddSingleton<IBotMessageManager, BotMessageManager>();
        services.AddSingleton<IHeartbeatManager,HeartbeatManager>();
        services.AddSingleton<BotSessionContext>();
        services.AddSingleton<SocketQQBot>();
        return services;
    }
}
