using Galaxy.QQBot.Bot;
using Galaxy.QQBot.Identity;
using Galaxy.QQBot.Tool;
using Microsoft.Extensions.DependencyInjection;

namespace Galaxy.QQBot.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddQQBot(this IServiceCollection services)
    {
        services.AddHttpClient<IHttpHelper, HttpHelper>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(30);
            client.DefaultRequestHeaders.Add("User-Agent", "Galaxy.QQBot/1.0");
            client.DefaultRequestHeaders.Add("Accept", "application/json");
        });
        services.AddSingleton<IWebSocketClient, WebSocketClient>();
        services.AddSingleton<IAccessTokenManager, AccessTokenManager>();
        services.AddSingleton<SocketQQBot>();
        return services;
    }
}
