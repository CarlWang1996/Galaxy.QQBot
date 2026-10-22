using Galaxy.QQBot.Bot;
using Microsoft.Extensions.Logging;
using Serilog;

namespace Galaxy.QQBot.Test;

public class Program
{
    static async Task Main(string[] args)
    {
        Log.Logger = new LoggerConfiguration().MinimumLevel.Debug().WriteTo.Console().CreateLogger();
        using var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.ClearProviders();
            builder.AddSerilog(Log.Logger,dispose:false);
            builder.SetMinimumLevel(LogLevel.Debug);
        });
        string appId = Environment.GetEnvironmentVariable("BotId", EnvironmentVariableTarget.Machine)!;
        string appSecret = Environment.GetEnvironmentVariable("BotSecret", EnvironmentVariableTarget.Machine)!;
        SocketQQBot bot = new SocketQQBot(appId, appSecret, 1, loggerFactory);
        await bot.ConnectAsync();
        await bot.LoginAsync();
        Console.Read();

        //var builder = Host.CreateApplicationBuilder();
        //builder.Services.AddSerilog();
        //builder.Services.AddQQBot(option => option.UseAppCredentials("102077650", "5Oi2Ni4QnBZyNnDe5X0TxRwRxT0Y6fEo").UseShards(1));
        //builder.Services.AddHostedService<HostService>();
        //var app = builder.Build();
        //await app.RunAsync();
    }
}
