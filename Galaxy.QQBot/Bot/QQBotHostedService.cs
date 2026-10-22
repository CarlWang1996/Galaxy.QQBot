using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Galaxy.QQBot.Bot;

public class QQBotHostedService : BackgroundService
{
    private readonly SocketQQBot _bot;
    private readonly ILogger<QQBotHostedService> _logger;

    public QQBotHostedService(SocketQQBot bot, ILogger<QQBotHostedService> logger)
    {
        _bot = bot;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("QQ Bot 宿主服务正在启动...");
        await _bot.ConnectAsync();
        await _bot.LoginAsync();

        try
        {
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("QQ Bot 宿主服务正在停止...");
        }
    }
}
