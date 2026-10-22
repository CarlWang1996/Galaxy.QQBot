using Galaxy.QQBot.Bot;
using Microsoft.Extensions.Logging;

namespace Galaxy.QQBot.Test;

public class Program
{
    static async Task Main(string[] args)
    {
        using var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddConsole().SetMinimumLevel(LogLevel.Debug);
        });
        SocketQQBot bot = new SocketQQBot("102077650", "5Oi2Ni4QnBZyNnDe5X0TxRwRxT0Y6fEo", 1, loggerFactory);
        await bot.ConnectAsync();
        await bot.LoginAsync();
        Console.Read();
    }
}
