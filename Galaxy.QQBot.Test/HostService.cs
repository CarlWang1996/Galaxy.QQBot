using Galaxy.QQBot.Bot;
using Microsoft.Extensions.Hosting;

namespace Galaxy.QQBot.Test;

public class HostService : BackgroundService
{
    private readonly SocketQQBot _bot;

    public HostService(SocketQQBot bot)
    {
        _bot = bot;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await _bot.ConnectAsync();
        await _bot.LoginAsync();
    }
}
