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
            builder.AddSerilog(dispose:true);
        });
        string appId = Environment.GetEnvironmentVariable("BotId", EnvironmentVariableTarget.Machine)!;
        string appSecret = Environment.GetEnvironmentVariable("BotSecret", EnvironmentVariableTarget.Machine)!;
        SocketQQBot bot = new SocketQQBot(appId, appSecret, loggerFactory);
        bot.C2cMessages.Subscribe(async s => await s.ReplyAsync(s.Content));
        await bot.StartAsync();
        Console.Read();
    }
}
