using Galaxy.QQBot.Bot;
using Galaxy.QQBot.Extensions;
using Galaxy.QQBot.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Galaxy.QQBot.Test;

public class Program
{
    static async Task Main(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(args);
        builder.Services.AddQQBot();
        builder.Services.AddLogging();
        var app = builder.Build();
        var bot = app.Services.GetRequiredService<SocketQQBot>();
        await bot.ConnectAsync();
        await bot.LoginAsync();
        await app.RunAsync();
    }
}
