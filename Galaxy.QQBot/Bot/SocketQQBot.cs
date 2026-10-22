using Galaxy.QQBot.Common;
using Galaxy.QQBot.EventData;
using Galaxy.QQBot.Manager;
using Galaxy.QQBot.Tool;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Galaxy.QQBot.Bot;

public class SocketQQBot : IDisposable
{
    internal record HelloData(int heartbeat_interval);
    internal record GatewayResponse(string url);
    private readonly QQBotOptions _options;
    private readonly BotSessionContext _context;
    private readonly ILogger _logger;
    private readonly IWebSocketClient _webSocket;
    private readonly IHttpHelper _httpHelper;
    private readonly IJsonHelper _jsonHelper;
    private readonly IAccessTokenManager _accessTokenManager;
    private readonly IBotMessageManager _botMessageManager;
    private readonly IHeartbeatManager _heartbeatManager;
    private readonly CancellationTokenSource _source = new();
    private int _reconnecting = 0;
    private readonly object _heartbeatLock = new();
    private Task? _heartbeatTask;
    private CancellationTokenSource? _heartbeatCts;
    private readonly object _tokenLock = new();
    private Task? _tokenRefreshTask;
    private CancellationTokenSource? _tokenRefreshCts;

    public SocketQQBot(
        IOptions<QQBotOptions> options,
        BotSessionContext context,
        ILogger<SocketQQBot> logger,
        IWebSocketClient webSocket,
        IHttpHelper httpHelper,
        IJsonHelper jsonHelper,
        IAccessTokenManager accessTokenManager,
        IBotMessageManager botMessageManager,
        IHeartbeatManager heartbeatService)
    {
        _options = options.Value;
        _context = context;
        _logger = logger;
        _webSocket = webSocket;
        _httpHelper = httpHelper;
        _jsonHelper = jsonHelper;
        _accessTokenManager = accessTokenManager;
        _botMessageManager = botMessageManager;
        _heartbeatManager = heartbeatService;

        if (string.IsNullOrEmpty(_options.AppId) || string.IsNullOrEmpty(_options.AppSecret))
        {
            throw new InvalidOperationException("QQBot 的 AppId 或 AppSecret 未在配置中设置。");
        }

        _webSocket.OnMessageReceived += HandleMessageAsync;
        _webSocket.OnDisconnected += HandleDisconnectedAsync;
    }

    public SocketQQBot(string appId, string appSecret, int shards, ILoggerFactory? factory = null)
    {
        factory = factory ?? new NullLoggerFactory();
        _options = new QQBotOptions() { AppId = appId, AppSecret = appSecret, Shards = shards };
        _context = new BotSessionContext();
        _logger = factory.CreateLogger<SocketQQBot>();
        _webSocket = new WebSocketClient();
        _httpHelper = new HttpHelper(new HttpClient());
        _jsonHelper = new JsonHelper();
        _accessTokenManager = new AccessTokenManager(_httpHelper, factory.CreateLogger<AccessTokenManager>());
        _botMessageManager = new BotMessageManager(_httpHelper, _accessTokenManager, _jsonHelper);
        _heartbeatManager = new HeartbeatManager(_webSocket, _context, factory.CreateLogger<HeartbeatManager>(), _jsonHelper);

        if (string.IsNullOrEmpty(_options.AppId) || string.IsNullOrEmpty(_options.AppSecret))
        {
            throw new InvalidOperationException("QQBot 的 AppId 或 AppSecret 未在配置中设置。");
        }

        _webSocket.OnMessageReceived += HandleMessageAsync;
        _webSocket.OnDisconnected += HandleDisconnectedAsync;
    }

    private async Task HandleDisconnectedAsync(string arg)
    {
        _logger.LogWarning("WebSocket 断开: {msg}", arg);
        if (arg.Contains("4914") || arg.Contains("4915") || arg.Contains("4013"))
        {
            _logger.LogError("机器人已下架/封禁或 Intent 无效，停止重连。");
            return;
        }

        if (arg.Contains("4009"))
        {
            _logger.LogInformation("连接过期，尝试 Resume 重连...");
        }
        else
        {
            _logger.LogInformation("尝试重连...");
        }

        await TryReconnectAsync();
    }

    private async Task TryReconnectAsync()
    {
        if (Interlocked.Exchange(ref _reconnecting, 1) == 1)
        {
            _logger.LogDebug("已有重连流程在运行，忽略本次触发。");
            return;
        }

        try
        {
            StopHeartbeatLoop();

            var retry = 0;
            while (!_source.IsCancellationRequested)
            {
                retry++;
                var delay = TimeSpan.FromSeconds(Math.Min(30, Math.Pow(2, retry - 1)));

                try
                {
                    await Task.Delay(delay, _source.Token);

                    await ConnectAsync();

                    if (!string.IsNullOrEmpty(_context.SessionId) && _context.LastS != null)
                    {
                        await ReconnectAsync();
                        _logger.LogInformation("已发起 Resume（第 {retry} 次尝试）", retry);
                    }
                    else
                    {
                        await LoginAsync();
                        _logger.LogInformation("已发起 Identify（第 {retry} 次尝试）", retry);
                    }

                    return;
                }
                catch (OperationCanceledException)
                {
                    return;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "第 {retry} 次重连失败，继续重试", retry);
                }
            }
        }
        finally
        {
            Interlocked.Exchange(ref _reconnecting, 0);
        }
    }

    private async Task HandleMessageAsync(string arg)
    {
        Payload payload = _jsonHelper.DeserializeElement<Payload>(arg)!;

        if (payload.Op == (int)OpCode.Hello)
        {
            var d = _jsonHelper.DeserializeElement<HelloData>(payload.D)!;
            _context.HeartBeatInterval = d.heartbeat_interval;
        }
        else if (payload.Op == (int)OpCode.Dispatch)
        {
            _context.LastS = payload.S;

            if (payload.T == "READY")
            {
                EnsureHeartbeatLoopStarted();

                try
                {
                    ReadyEventData readyData = _jsonHelper.DeserializeElement<ReadyEventData>(payload.D.ToString()!)!;
                    _logger.LogDebug("收到ready信息:{@data}", readyData);
                    _context.SessionId = readyData.SessionId;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "解析或推送 READY 事件失败");
                }
            }
            else if (payload.T == "RESUMED")
            {
                _logger.LogDebug("重连成功 (RESUMED)，恢复接收事件");
            }
            else if (payload.T == "C2C_MESSAGE_CREATE")
            {
                C2cEventData data = _jsonHelper.DeserializeElement<C2cEventData>(payload.D.ToString()!)!;
                _logger.LogDebug("收到私聊消息:{@data}", data);
                // TODO: 处理单聊消息
            }
            else if (payload.T == "GROUP_AT_MESSAGE_CREATE")
            {
                GroupEventData data = _jsonHelper.DeserializeElement<GroupEventData>(payload.D.ToString()!)!;
                _logger.LogDebug("收到群聊消息:{@data}", data);
                // TODO: 处理群聊艾特
            }
            else if (payload.T == "GROUP_MESSAGE_CREATE")
            {
                GroupEventData data = _jsonHelper.DeserializeElement<GroupEventData>(payload.D.ToString()!)!;
                // TODO: 处理群聊消息
            }
            else if (payload.T == "INTERACTION_CREATE")
            {
                InteractionEventData data = _jsonHelper.DeserializeElement<InteractionEventData>(payload.D.ToString()!)!;
                _logger.LogDebug("收到按钮交互消息:{@data}", data);
                // TODO: 处理交互事件
            }
        }
    }

    public async Task ConnectAsync()
    {
        if (_webSocket.IsConnected)
        {
            return;
        }

        EnsureTokenRefreshLoopStarted();

        await _accessTokenManager.WaitForTokenAsync(_source.Token);

        string obj = await _httpHelper.GetAsync($"{Constants.BaseUrl}gateway", _accessTokenManager.AccessToken);
        GatewayResponse response = _jsonHelper.DeserializeElement<GatewayResponse>(obj)!;
        await _webSocket.ConnectAsync(response.url);
        _logger.LogInformation("连接成功");
    }

    public async Task LoginAsync()
    {
        Payload payload = new Payload()
        {
            Op = 2,
            D = new
            {
                token = $"QQBot {_accessTokenManager.AccessToken}",
                intents = Intents.GROUP_AND_C2C_EVENT | Intents.INTERACTION,
                shard = new int[] { 0, _options.Shards },
                properties = new { }
            }
        };
        string json = _jsonHelper.Serialize(payload);
        await _webSocket.SendAsync(json);
    }

    private async Task ReconnectAsync()
    {
        Payload pay = new Payload()
        {
            Op = (int)OpCode.Resume,
            D = new
            {
                token = $"QQBot {_accessTokenManager.AccessToken}",
                session_id = _context.SessionId,
                seq = _context.LastS
            }
        };
        string json = _jsonHelper.Serialize(pay);
        await _webSocket.SendAsync(json);
    }

    private void EnsureHeartbeatLoopStarted()
    {
        lock (_heartbeatLock)
        {
            if (_heartbeatTask != null && !_heartbeatTask.IsCompleted)
            {
                _logger.LogDebug("心跳循环已在运行，跳过启动。");
                return;
            }

            _heartbeatCts?.Dispose();
            _heartbeatCts = CancellationTokenSource.CreateLinkedTokenSource(_source.Token);

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
                    _logger.LogError(ex, "心跳循环异常退出");
                }
            }, token);

            _logger.LogInformation("已启动心跳循环。");
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

            try { _heartbeatCts.Cancel(); } catch { }
            _heartbeatCts.Dispose();
            _heartbeatCts = null;
            _heartbeatTask = null;
            _logger.LogInformation("已停止心跳循环。");
        }
    }

    private void EnsureTokenRefreshLoopStarted()
    {
        lock (_tokenLock)
        {
            if (_tokenRefreshTask != null && !_tokenRefreshTask.IsCompleted)
            {
                _logger.LogDebug("Token 刷新循环已在运行，复用现有任务。");
                return;
            }

            _tokenRefreshCts?.Dispose();
            _tokenRefreshCts = CancellationTokenSource.CreateLinkedTokenSource(_source.Token);

            var token = _tokenRefreshCts.Token;
            var appId = _options.AppId;
            var appSecret = _options.AppSecret;

            _tokenRefreshTask = Task.Run(async () =>
            {
                try
                {
                    await _accessTokenManager.StartGetAccessToken(appId, appSecret, token);
                }
                catch (OperationCanceledException)
                {
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Token 刷新循环异常退出");
                }
            }, token);

            _logger.LogInformation("已启动 Token 刷新循环。");
        }
    }

    private void StopTokenRefreshLoop()
    {
        lock (_tokenLock)
        {
            if (_tokenRefreshCts == null)
            {
                return;
            }

            try { _tokenRefreshCts.Cancel(); } catch { }
            _tokenRefreshCts.Dispose();
            _tokenRefreshCts = null;
            _tokenRefreshTask = null;
            _logger.LogInformation("已停止 Token 刷新循环。");
        }
    }

    public void Dispose()
    {
        _source.Cancel();

        StopHeartbeatLoop();
        StopTokenRefreshLoop();

        try
        {
            _webSocket.DisposeAsync().AsTask().GetAwaiter().GetResult();
        }
        catch
        {
        }

        _source.Dispose();
    }
}