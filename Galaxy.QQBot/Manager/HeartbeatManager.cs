using Galaxy.QQBot.Bot;
using Galaxy.QQBot.Common;
using Galaxy.QQBot.Tool;
using Microsoft.Extensions.Logging;

namespace Galaxy.QQBot.Manager;

internal class HeartbeatManager
{
    private readonly WebSocketClient _webSocket;
    private readonly BotSessionContext _context;
    private readonly ILogger _logger;

    internal HeartbeatManager(WebSocketClient webSocket, BotSessionContext context, ILoggerFactory factory)
    {
        _webSocket = webSocket;
        _context = context;
        _logger = factory.CreateLogger<HeartbeatManager>();
    }

    public async Task RunAsync(CancellationToken token)
    {
        _logger.LogDebug("心跳任务已启动，间隔: {interval}ms", _context.HeartBeatInterval);

        while (!token.IsCancellationRequested)
        {
            try
            {
                Payload pay = new Payload()
                {
                    Op = (int)OpCode.Heartbeat,
                    D = _context.LastS!
                };
                string json = JsonHelper.Serialize(pay);
                await _webSocket.SendAsync(json);
                await Task.Delay(_context.HeartBeatInterval, token);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "发送心跳包时发生异常");
                await Task.Delay(1000, token);
            }
        }

        _logger.LogDebug("心跳任务已停止");
    }
}

