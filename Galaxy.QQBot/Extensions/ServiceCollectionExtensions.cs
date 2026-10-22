using Galaxy.QQBot.Bot;
using Galaxy.QQBot.Manager;
using Galaxy.QQBot.Tool;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Galaxy.QQBot.Extensions;

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
        });
        services.AddSingleton<IWebSocketClient, WebSocketClient>();
        services.AddSingleton<IAccessTokenManager, AccessTokenManager>();
        services.AddSingleton<IBotMessageManager, BotMessageManager>();
        services.AddSingleton<SocketQQBot>();
        services.AddHostedService<QQBotHostedService>();
        return services;
    }
}
