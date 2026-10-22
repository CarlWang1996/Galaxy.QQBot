using Galaxy.QQBot.Bot;
using Microsoft.Extensions.Logging;
using Serilog;

namespace Galaxy.QQBot.Test;

public class Program
{
    static async Task Main(string[] args)
    {
        Log.Logger = new LoggerConfiguration().MinimumLevel.Information().WriteTo.Console().CreateLogger();
        using var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddSerilog(dispose:true);
        });
        string appId = Environment.GetEnvironmentVariable("BotId", EnvironmentVariableTarget.Machine)!;
        string appSecret = Environment.GetEnvironmentVariable("BotSecret", EnvironmentVariableTarget.Machine)!;
        SocketQQBot bot = new SocketQQBot(appId, appSecret, loggerFactory);
        bot.GroupAtMessages.Subscribe(async s => 
        { 
            await s.MessageManager.SendGroupMessageAsync(s.GroupOpenid, "这是一条主动消息");
        });
        await bot.StartAsync();
        Console.Read();
    }
}
