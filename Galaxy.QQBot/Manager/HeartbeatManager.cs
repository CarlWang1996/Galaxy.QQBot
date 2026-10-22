using Galaxy.QQBot.Bot;
using Galaxy.QQBot.Common;
using Galaxy.QQBot.Tool;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Galaxy.QQBot.Manager;

public interface IHeartbeatManager
{
    Task RunAsync(CancellationToken token);
}

public class HeartbeatManager: IHeartbeatManager
{
    private readonly IWebSocketClient _webSocket;
    private readonly BotSessionContext _context;
    private readonly ILogger _logger;

    public HeartbeatManager(
        IWebSocketClient webSocket,
        BotSessionContext context,
        ILogger<HeartbeatManager> logger)
    {
        _webSocket = webSocket;
        _context = context;
        _logger = logger;
    }

    public async Task RunAsync(CancellationToken token)
    {
        _logger.LogInformation("心跳任务已启动，间隔: {interval}ms", _context.HeartBeatInterval);

        while (!token.IsCancellationRequested)
        {
            try
            {
                // 从上下文动态获取最新的 LastS
                Payload pay = new Payload()
                {
                    op = (int)OpCode.Heartbeat,
                    d = _context.LastS!
                };
                string json = JsonConvert.SerializeObject(pay);

                await _webSocket.SendAsync(json);

                // 使用配置的间隔
                await Task.Delay(_context.HeartBeatInterval, token);
            }
            catch (OperationCanceledException)
            {
                // 正常取消，退出循环
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "发送心跳包时发生异常");
                // 防止死循环疯狂报错，稍微延迟一下
                await Task.Delay(1000, token);
            }
        }

        _logger.LogInformation("心跳任务已停止");
    }
}

