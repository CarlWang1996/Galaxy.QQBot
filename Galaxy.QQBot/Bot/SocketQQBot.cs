using Galaxy.QQBot.Common;
using Galaxy.QQBot.EventData;
using Galaxy.QQBot.Manager;
using Galaxy.QQBot.Tool;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Galaxy.QQBot.Bot;

public class SocketQQBot : IDisposable
{
    private readonly IAccessTokenManager _accessTokenManager;
    private readonly IWebSocketClient _webSocket;
    private readonly IHttpHelper _httpHelper;
    private readonly ILogger _logger;
    private readonly QQBotOptions _options;
    private readonly IBotMessageManager _botMessageManager;
    private readonly BotSessionContext _context;
    private readonly IHeartbeatManager _heartbeatManager;

    private CancellationTokenSource _source = new CancellationTokenSource();

    public SocketQQBot(
        IWebSocketClient webSocket,
        IHttpHelper httpHelper,
        IAccessTokenManager accessTokenManager,
        ILogger<SocketQQBot> logger,
        IOptions<QQBotOptions> options,
        IBotMessageManager botMessageManager,
        BotSessionContext context,
        IHeartbeatManager heartbeatService)
    {
        _webSocket = webSocket;
        _httpHelper = httpHelper;
        _accessTokenManager = accessTokenManager;
        _logger = logger;
        _botMessageManager = botMessageManager;
        _options = options.Value;
        _context = context;
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
        _options = new QQBotOptions() { AppId = appId, AppSecret = appSecret, Shards = shards };
        _context = new BotSessionContext();
        factory = factory ?? new NullLoggerFactory();
        _webSocket = new WebSocketClient();
        _httpHelper = new HttpHelper(new HttpClient());
        _logger = factory.CreateLogger<SocketQQBot>();
        _accessTokenManager = new AccessTokenManager(_httpHelper,factory.CreateLogger<AccessTokenManager>());
        _botMessageManager = new BotMessageManager(_httpHelper, _accessTokenManager);
        _heartbeatManager = new HeartbeatManager(_webSocket, _context, factory.CreateLogger<HeartbeatManager>());
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
        Payload payload = JsonConvert.DeserializeObject<Payload>(arg)!;

        if (payload.op == (int)OpCode.Hello)
        {
            _context.HeartBeatInterval = payload.d.heartbeat_interval;
        }
        else if (payload.op == (int)OpCode.Dispatch)
        {
            _context.LastS = payload.s;

            if (payload.t == "READY")
            {
                _ = _heartbeatManager.RunAsync(_source.Token);

                try
                {
                    ReadyEventData readyData = JsonConvert.DeserializeObject<ReadyEventData>(payload.d.ToString());
                    _context.SessionId = readyData.session_id;
                    _logger.LogInformation("Bot 登录成功 (READY)，SessionId: {sid}", readyData.session_id);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "解析或推送 READY 事件失败");
                }
            }
            else if (payload.t == "RESUMED")
            {
                _logger.LogInformation("重连成功 (RESUMED)，恢复接收事件");
            }
            else if (payload.t == "C2C_MESSAGE_CREATE")
            {
                C2cEventData data = JsonConvert.DeserializeObject<C2cEventData>(payload.d.ToString());
                // TODO: 处理单聊消息
            }
            else if (payload.t == "GROUP_AT_MESSAGE_CREATE")
            {
                GroupEventData data = JsonConvert.DeserializeObject<GroupEventData>(payload.d.ToString());
                // TODO: 处理群聊艾特
            }
            else if (payload.t == "GROUP_MESSAGE_CREATE")
            {
                GroupEventData data = JsonConvert.DeserializeObject<GroupEventData>(payload.d.ToString());
                // TODO: 处理群聊消息
            }
            else if (payload.t == "INTERACTION_CREATE")
            {
                InteractionEventData data = JsonConvert.DeserializeObject<InteractionEventData>(payload.d.ToString());
                // TODO: 处理交互事件
            }
        }
    }

    public async Task ConnectAsync()
    {
        _ = _accessTokenManager.StartGetAccessToken(_options.AppId, _options.AppSecret, _source.Token);
        await _accessTokenManager.WaitForTokenAsync(_source.Token);
        string obj = await _httpHelper.GetAsync($"{Constants.BaseUrl}gateway", _accessTokenManager.AccessToken);
        JObject response = JObject.Parse(obj);
        await _webSocket.ConnectAsync(response["url"]!.Value<string>()!);
        _logger.LogInformation("连接成功");
    }

    public async Task LoginAsync()
    {
        Payload payload = new Payload()
        {
            op = 2,
            d = new
            {
                token = $"QQBot {_accessTokenManager.AccessToken}",
                intents = Intents.GROUP_AND_C2C_EVENT | Intents.INTERACTION,
                shard = new int[] { 0, _options.Shards },
                properties = new { }
            }
        };
        string json = JsonConvert.SerializeObject(payload);
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
            op = (int)OpCode.Resume,
            d = new
            {
                token = $"QQBot {_accessTokenManager.AccessToken}",
                session_id = _context.SessionId,
                seq = _context.LastS
            }
        };
        string json = JsonConvert.SerializeObject(pay);
        await _webSocket.SendAsync(json);
    }
}