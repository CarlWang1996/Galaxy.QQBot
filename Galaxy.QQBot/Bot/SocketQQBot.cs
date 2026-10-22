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

    private CancellationTokenSource _source = new CancellationTokenSource();

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

    public SocketQQBot(string appId, string appSecret, int shards,ILoggerFactory? factory = null)
    {
        factory = factory ?? new NullLoggerFactory();
        _options = new QQBotOptions() { AppId = appId, AppSecret = appSecret, Shards = shards };
        _context = new BotSessionContext();
        _logger = factory.CreateLogger<SocketQQBot>();
        _webSocket = new WebSocketClient();
        _httpHelper = new HttpHelper(new HttpClient());
        _jsonHelper = new JsonHelper();
        _accessTokenManager = new AccessTokenManager(_httpHelper,factory.CreateLogger<AccessTokenManager>());
        _botMessageManager = new BotMessageManager(_httpHelper, _accessTokenManager,_jsonHelper);
        _heartbeatManager = new HeartbeatManager(_webSocket, _context, factory.CreateLogger<HeartbeatManager>(),_jsonHelper);
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
            _logger.LogError("机器人已下架或被封禁或 Intent 无效，停止重连。");
            return;
        }

        if (arg.Contains("4009"))
        {
            _logger.LogInformation("连接过期，尝试重连并 Resume...");
            await ReconnectAsync();
            return;
        }

        _logger.LogInformation("尝试重连并重新登录...");
        await ConnectAsync();

        if (!string.IsNullOrEmpty(_context.SessionId) && _context.LastS != null)
        {
            await ReconnectAsync();
        }
        else
        {
            await LoginAsync();
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
                _ = _heartbeatManager.RunAsync(_source.Token);

                try
                {
                    ReadyEventData readyData = _jsonHelper.DeserializeElement<ReadyEventData>(payload.D.ToString()!)!;
                    _context.SessionId = readyData.session_id;
                    _logger.LogInformation("Bot 登录成功 (READY)，SessionId: {sid}", readyData.session_id);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "解析或推送 READY 事件失败");
                }
            }
            else if (payload.T == "RESUMED")
            {
                _logger.LogInformation("重连成功 (RESUMED)，恢复接收事件");
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
                // TODO: 处理交互事件
            }
        }
    }

    public async Task ConnectAsync()
    {
        _ = _accessTokenManager.StartGetAccessToken(_options.AppId, _options.AppSecret, _source.Token);
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

    public void Dispose()
    {
        _source.Cancel();
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
}