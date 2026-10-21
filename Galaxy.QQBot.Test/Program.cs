using Galaxy.QQBot.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Galaxy.QQBot.Test;

public class Program
{
    static async Task Main(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(args);
        builder.Services.AddQQBot(builder => 
        {
            builder.UseAppCredentials("102077650", "5Oi2Ni4QnBZyNnDe5X0TxRwRxT0Y6fEo").UseShards(1);
        });
        builder.Services.AddLogging();
        var app = builder.Build();
        await app.RunAsync();
    }
}
