using Galaxy.QQBot.Common;
using Galaxy.QQBot.EventData;
using Galaxy.QQBot.Manager;
using Galaxy.QQBot.Tool;
using Microsoft.Extensions.Logging;

namespace Galaxy.QQBot.Bot;

internal sealed class ShardConnection : IAsyncDisposable
{
    private const int InitialConnectMaxAttempts = 5;

    private record GatewayResponse(string url);
    private record HelloData(int heartbeat_interval);
    private readonly int _shardId;
    private readonly int _shardTotal;
    private readonly BotSessionContext _context;
    private readonly ILogger _logger;
    private readonly WebSocketClient _webSocket;
    private readonly HttpHelper _httpHelper;
    private readonly AccessTokenManager _accessTokenManager;
    private readonly HeartbeatManager _heartbeatManager;
    private readonly CancellationTokenSource _linkedSource;
    private readonly BotMessageManager _botMessageManager;
    private int _reconnecting = 0;
    private readonly object _reconnectTaskLock = new();
    private Task? _reconnectTask;
    private readonly object _heartbeatLock = new();
    private Task? _heartbeatTask;
    private CancellationTokenSource? _heartbeatCts;

    private volatile bool _terminal;

    private readonly Action<C2cEventData>? _onC2cMessage;
    private readonly Action<GroupEventData>? _onGroupAtMessage;
    private readonly Action<GroupEventData>? _onGroupMessage;
    private readonly Action<InteractionEventData>? _onInteraction;
    private readonly Action<ReadyEventData>? _onReady;

    public ShardConnection(
        int shardId, 
        int shardTotal, 
        ILoggerFactory factory, 
        HttpHelper httpHelper, 
        AccessTokenManager accessTokenManager, 
        CancellationToken parentToken,
        BotMessageManager botMessageManager,
        Action<C2cEventData>? onC2cMessage,
        Action<GroupEventData>? onGroupAtMessage,
        Action<GroupEventData>? onGroupMessage,
        Action<InteractionEventData>? onInteraction,
        Action<ReadyEventData>? onReady)
    {
        _shardId = shardId;
        _shardTotal = shardTotal;
        _logger = factory.CreateLogger<ShardConnection>();
        _httpHelper = httpHelper;
        _accessTokenManager = accessTokenManager;
        _linkedSource = CancellationTokenSource.CreateLinkedTokenSource(parentToken);
        _botMessageManager = botMessageManager;
        _context = new BotSessionContext();
        _webSocket = new WebSocketClient();
        _webSocket.OnMessageReceived += HandleMessageAsync;
        _webSocket.OnDisconnected += HandleDisconnectedAsync;
        _heartbeatManager = new HeartbeatManager(_webSocket, _context, factory);
        _onC2cMessage = onC2cMessage;
        _onGroupAtMessage = onGroupAtMessage;
        _onGroupMessage = onGroupMessage;
        _onInteraction = onInteraction;
        _onReady = onReady;
    }

    public async Task StartAsync()
    {
        Task task = TryReconnectAsync(InitialConnectMaxAttempts, immediateFirstAttempt: true);
        lock (_reconnectTaskLock)
        {
            if (_reconnectTask is null or { IsCompleted: true })
            {
                _reconnectTask = task;
            }
        }

        await task;
    }

    private async Task ConnectAsync()
    {
        if (_webSocket.IsConnected)
        {
            return;
        }

        await _accessTokenManager.WaitForTokenAsync(_linkedSource.Token);
        string obj = await _httpHelper.GetAsync($"{Constants.BaseUrl}gateway", _accessTokenManager.AccessToken, _linkedSource.Token);
        var response = JsonHelper.DeserializeElement<GatewayResponse>(obj)!;
        await _webSocket.ConnectAsync(response.url, _linkedSource.Token);
        _logger.LogInformation("[shard {id}/{total}] 连接成功", _shardId, _shardTotal);
    }

    private async Task IdentifyAsync()
    {
        Payload payload = new Payload
        {
            Op = (int)OpCode.Identify,
            D = new
            {
                token = $"QQBot {_accessTokenManager.AccessToken}",
                intents = Intents.GROUP_AND_C2C_EVENT | Intents.INTERACTION,
                shard = new int[] { _shardId, _shardTotal },
                properties = new { }
            }
        };

        string json = JsonHelper.Serialize(payload);
        await _webSocket.SendAsync(json);
    }

    private async Task ResumeAsync()
    {
        Payload pay = new Payload
        {
            Op = (int)OpCode.Resume,
            D = new
            {
                token = $"QQBot {_accessTokenManager.AccessToken}",
                session_id = _context.SessionId,
                seq = _context.LastS
            }
        };

        string json = JsonHelper.Serialize(pay);
        await _webSocket.SendAsync(json);
    }

    private async Task HandleDisconnectedAsync(string arg)
    {
        _logger.LogWarning("[shard {id}/{total}] WebSocket 断开: {msg}", _shardId, _shardTotal, arg);

        if (arg.Contains("4914") || arg.Contains("4915") || arg.Contains("4013") || arg.Contains("4014"))
        {
            _terminal = true;
            StopHeartbeatLoop();
            _logger.LogError("[shard {id}] 机器人已下架/封禁/Intent 无效或无权限，停止重连。", _shardId);
            return;
        }

        if (arg.Contains("4006") || arg.Contains("4007") ||
            arg.Contains("4900") || arg.Contains("4901") || arg.Contains("4902") || arg.Contains("4903") ||
            arg.Contains("4904") || arg.Contains("4905") || arg.Contains("4906") || arg.Contains("4907") ||
            arg.Contains("4908") || arg.Contains("4909") || arg.Contains("4910") || arg.Contains("4911") ||
            arg.Contains("4912") || arg.Contains("4913"))
        {
            _logger.LogDebug("[shard {id}] 错误码要求重新 Identify，清空 session。", _shardId);
            _context.SessionId = null;
            _context.LastS = null;
        }
        else if (arg.Contains("4009"))
        {
            _logger.LogDebug("[shard {id}] 连接过期，尝试 Resume 重连...", _shardId);
        }
        else
        {
            _logger.LogDebug("[shard {id}] 尝试重连...", _shardId);
        }

        ScheduleReconnect();
    }

    private async Task TryReconnectAsync(int maxAttempts = int.MaxValue, bool immediateFirstAttempt = false)
    {
        if (Interlocked.Exchange(ref _reconnecting, 1) == 1)
        {
            _logger.LogDebug("[shard {id}] 已有重连流程在运行，忽略。", _shardId);
            return;
        }

        Exception? lastError = null;
        var exhausted = false;

        try
        {
            StopHeartbeatLoop();
            var retry = 0;

            while (!_linkedSource.IsCancellationRequested && !_terminal)
            {
                if (retry >= maxAttempts)
                {
                    exhausted = true;
                    throw new InvalidOperationException($"[shard {_shardId}] 连接失败，已尝试 {retry} 次。", lastError);
                }

                retry++;
                var delay = immediateFirstAttempt && retry == 1
                    ? TimeSpan.Zero
                    : TimeSpan.FromSeconds(Math.Min(30, Math.Pow(2, retry - 1)));

                try
                {
                    await Task.Delay(delay, _linkedSource.Token);
                    await ConnectAsync();
                    if (!string.IsNullOrEmpty(_context.SessionId) && _context.LastS != null)
                    {
                        await ResumeAsync();
                        EnsureHeartbeatLoopStarted();
                        _logger.LogDebug("[shard {id}] 已发起 Resume（第 {retry} 次）", _shardId, retry);
                    }
                    else
                    {
                        await IdentifyAsync();
                        _logger.LogDebug("[shard {id}] 已发起 Identify（第 {retry} 次）", _shardId, retry);
                    }

                    return;
                }
                catch (OperationCanceledException)
                {
                    return;
                }
                catch (Exception ex)
                {
                    lastError = ex;
                    _logger.LogWarning(ex, "[shard {id}] 第 {retry} 次重连失败，继续重试", _shardId, retry);
                }
            }

            if (_terminal && !_linkedSource.IsCancellationRequested)
            {
                exhausted = true;
                throw new InvalidOperationException($"[shard {_shardId}] 已进入终止状态，停止连接。");
            }
        }
        finally
        {
            Interlocked.Exchange(ref _reconnecting, 0);

            if (!exhausted && !_terminal && !_linkedSource.IsCancellationRequested && !_webSocket.IsConnected)
            {
                ScheduleReconnectFromCurrent();
            }
        }
    }

    private void ScheduleReconnect()
    {
        lock (_reconnectTaskLock)
        {
            if (_reconnectTask is { IsCompleted: false })
            {
                return;
            }

            _reconnectTask = ReconnectSafeAsync();
        }
    }

    private void ScheduleReconnectFromCurrent()
    {
        lock (_reconnectTaskLock)
        {
            _reconnectTask = ReconnectSafeAsync();
        }
    }

    private async Task ReconnectSafeAsync()
    {
        try
        {
            await TryReconnectAsync();
        }
        catch (OperationCanceledException)
        {
            // 正常停止
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[shard {id}] 重连流程异常退出", _shardId);
        }
    }

    private async Task HandleMessageAsync(string arg)
    {
        Payload? payload;

        try
        {
            payload = JsonHelper.DeserializeElement<Payload>(arg);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[shard {id}] 解析 Payload 失败，原始消息: {raw}", _shardId, arg);
            return;
        }

        if (payload == null)
        {
            _logger.LogWarning("[shard {id}] Payload 反序列化为 null，原始消息: {raw}", _shardId, arg);
            return;
        }

        switch ((OpCode)payload.Op)
        {
            case OpCode.Hello:
                try
                {
                    var d = JsonHelper.DeserializeElement<HelloData>(payload.D)!;
                    _context.HeartBeatInterval = d.heartbeat_interval;
                    _logger.LogDebug("[shard {id}] 收到 Hello，心跳间隔 {interval}ms", _shardId, d.heartbeat_interval);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[shard {id}] 解析 Hello 失败", _shardId);
                }

                break;

            case OpCode.Reconnect:
                _logger.LogDebug("[shard {id}] 服务端要求重连 (op=7)", _shardId);

                _ = Task.Run(async () =>
                {
                    try 
                    {
                        await _webSocket.DisconnectAsync();
                    }
                    catch (Exception ex) 
                    {
                        _logger.LogWarning(ex, "[shard {id}] 主动断开失败", _shardId);
                    }

                    await TryReconnectAsync();
                });

                break;

            case OpCode.Invalid_Session:
                _logger.LogDebug("[shard {id}] Session 失效 (op=9)，清空会话并重新 Identify", _shardId);
                _context.SessionId = null;
                _context.LastS = null;

                _ = Task.Run(async () =>
                {
                    try 
                    { 
                        await _webSocket.DisconnectAsync(); 
                    }
                    catch (Exception ex) 
                    { 
                        _logger.LogWarning(ex, "[shard {id}] 主动断开失败", _shardId); 
                    }

                    await TryReconnectAsync();
                });

                break;

            case OpCode.Heartbeat:
                _logger.LogDebug("[shard {id}] 收到服务端心跳请求 (op=1)，立即回应", _shardId);
                _ = SendHeartbeatAsync();
                break;

            case OpCode.HeartbeatACK:
                _logger.LogDebug("[shard {id}] 收到心跳 ACK (op=11)", _shardId);
                break;

            case OpCode.Dispatch:
                await HandleDispatchAsync(payload);
                break;

            default:
                _logger.LogDebug("[shard {id}] 未处理的 OpCode: {op}", _shardId, payload.Op);
                break;
        }
    }

    private async Task HandleDispatchAsync(Payload payload)
    {
        _context.LastS = payload.S;

        try
        {
            switch (payload.T)
            {
                case "READY":
                    EnsureHeartbeatLoopStarted();
                    try
                    {
                        ReadyEventData readyData = JsonHelper.DeserializeElement<ReadyEventData>(payload.D.ToString()!)!;
                        _logger.LogDebug("[shard {id}] 收到ready信息:{@data}", _shardId, readyData);
                        _logger.LogInformation("登录成功");
                        _context.SessionId = readyData.SessionId;
                        _onReady?.Invoke(readyData);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "[shard {id}] 解析 READY 事件失败", _shardId);
                    }
                    break;

                case "RESUMED":
                    _logger.LogDebug("[shard {id}] 重连成功 (RESUMED)，恢复接收事件", _shardId);
                    break;

                case "C2C_MESSAGE_CREATE":
                    try
                    {
                        var data = JsonHelper.DeserializeElement<C2cEventData>(payload.D.ToString()!)!;
                        data.MessageManager = _botMessageManager;
                        _logger.LogDebug("[shard {id}] 收到私聊消息:{@data}", _shardId, data);
                        _onC2cMessage?.Invoke(data);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "[shard {id}] 解析 C2C_MESSAGE_CREATE 失败", _shardId);
                    }
                    break;

                case "GROUP_AT_MESSAGE_CREATE":
                    try
                    {
                        var data = JsonHelper.DeserializeElement<GroupEventData>(payload.D.ToString()!)!;
                        data.MessageManager = _botMessageManager;
                        _logger.LogDebug("[shard {id}] 收到群聊@消息:{@data}", _shardId, data);
                        _onGroupAtMessage?.Invoke(data);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "[shard {id}] 解析 GROUP_AT_MESSAGE_CREATE 失败", _shardId);
                    }
                    break;

                case "GROUP_MESSAGE_CREATE":
                    try
                    {
                        var data = JsonHelper.DeserializeElement<GroupEventData>(payload.D.ToString()!)!;
                        data.MessageManager = _botMessageManager;
                        _logger.LogDebug("[shard {id}] 收到群聊消息:{@data}", _shardId, data);
                        _onGroupMessage?.Invoke(data);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "[shard {id}] 解析 GROUP_MESSAGE_CREATE 失败", _shardId);
                    }
                    break;

                case "INTERACTION_CREATE":
                    try
                    {
                        var data = JsonHelper.DeserializeElement<InteractionEventData>(payload.D.ToString()!)!;
                        data.MessageManager = _botMessageManager;
                        _logger.LogDebug("[shard {id}] 收到按钮交互消息:{@data}", _shardId, data);
                        _onInteraction?.Invoke(data);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "[shard {id}] 解析 INTERACTION_CREATE 失败", _shardId);
                    }
                    break;

                default:
                    _logger.LogDebug("[shard {id}] 未处理的 Dispatch 事件: {type}", _shardId, payload.T);
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[shard {id}] 处理 Dispatch 事件 {type} 失败", _shardId, payload.T);
        }
    }

    private async Task SendHeartbeatAsync()
    {
        try
        {
            var payload = new Dictionary<string, object?>
            {
                ["op"] = (int)OpCode.Heartbeat,
                ["d"] = _context.LastS
            };
            string json = JsonHelper.Serialize(payload);
            await _webSocket.SendAsync(json);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[shard {id}] 发送心跳失败", _shardId);
        }
    }

    private void EnsureHeartbeatLoopStarted()
    {
        lock (_heartbeatLock)
        {
            if (_terminal) return;

            if (_context.HeartBeatInterval <= 0)
            {
                throw new InvalidOperationException("心跳间隔必须大于0");
            }

            if (_heartbeatTask != null && !_heartbeatTask.IsCompleted) return;

            _heartbeatCts?.Dispose();
            _heartbeatCts = CancellationTokenSource.CreateLinkedTokenSource(_linkedSource.Token);
            var token = _heartbeatCts.Token;

            _heartbeatTask = Task.Run(async () =>
            {
                try 
                {
                    await _heartbeatManager.RunAsync(token);
                }
                catch (OperationCanceledException) 
                {
                }
                catch (Exception ex) 
                {
                    _logger.LogError(ex, "[shard {id}] 心跳循环异常退出", _shardId);
                }
            }, token);

            _logger.LogDebug("[shard {id}] 已启动心跳循环。", _shardId);
        }
    }

    private void StopHeartbeatLoop()
    {
        lock (_heartbeatLock)
        {
            if (_heartbeatCts == null)
            {
                return;
            }
 
            try
            {
                _heartbeatCts.Cancel();
            }
            catch
            {
            }

            _heartbeatCts.Dispose();
            _heartbeatCts = null;
            _heartbeatTask = null;
            _logger.LogDebug("[shard {id}] 已停止心跳循环。", _shardId);
        }
    }

    public async ValueTask DisposeAsync()
    {
        _linkedSource.Cancel();
        StopHeartbeatLoop();

        Task? reconnectTask;
        lock (_reconnectTaskLock)
        {
            reconnectTask = _reconnectTask;
        }

        if (reconnectTask != null)
        {
            try
            {
                await reconnectTask;
            }
            catch
            {
            }
        }

        StopHeartbeatLoop();

        try
        {
            await _webSocket.DisposeAsync();
        }
        catch
        {
        }

        _linkedSource.Dispose();
    }
}