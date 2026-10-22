using Galaxy.QQBot.Common;
using Galaxy.QQBot.EventData;
using Galaxy.QQBot.Manager;
using Galaxy.QQBot.Tool;
using Microsoft.Extensions.Logging;
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
    private CancellationTokenSource _source = new CancellationTokenSource();
    private int? _lastS = null;
    private int _heartBeatInterval = 0;
    private string sessionId = string.Empty;
    private static readonly object sLock = new object();
    private readonly QQBotOptions _options;
    private readonly IBotMessageManager _botMessageManager;

    public SocketQQBot(
        IWebSocketClient webSocket, 
        IHttpHelper httpHelper,
        IAccessTokenManager accessTokenManager,
        ILogger<SocketQQBot> logger,
        IOptions<QQBotOptions> options,
        IBotMessageManager botMessageManager)
    {
        _webSocket = webSocket;
        _httpHelper = httpHelper;
        _accessTokenManager = accessTokenManager;
        _logger = logger;
        _botMessageManager = botMessageManager;
        _options = options.Value;
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
        if (!string.IsNullOrEmpty(sessionId) && _lastS != null)
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
            _heartBeatInterval = payload.d.heartbeat_interval;
        }
        else if (payload.op == (int)OpCode.HeartbeatACK)
        {
            
        }
        else if(payload.op == (int)OpCode.Dispatch)
        {
            _lastS = payload.s;
            if (payload.t == "READY")
            {
                _ = StartHeartBeatAsync(_source.Token);
                try
                {
                    ReadyEventData readyData = JsonConvert.DeserializeObject<ReadyEventData>(payload.d.ToString());
                    sessionId = readyData.session_id;
                    _logger.LogInformation("Bot 登录成功 (READY)，SessionId: {sid}", readyData.session_id);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "解析或推送 READY 事件失败");
                }
            }
            else if (payload.t == "RESUMED")
            {
                _logger.LogInformation("收到重连回复");
            }
            else if (payload.t == "C2C_MESSAGE_CREATE")
            {
                C2cEventData data = JsonConvert.DeserializeObject<C2cEventData>(payload.d.ToString());
                
            }
            else if(payload.t == "GROUP_AT_MESSAGE_CREATE")
            {
                GroupEventData data = JsonConvert.DeserializeObject<GroupEventData>(payload.d.ToString());
                await _botMessageManager.SendGroupMessageAsync(data.group_openid, messageId: data.id, sequence: 1, filePath: "F:\\工具\\1串口工具\\XCOM V2.0.exe");
            }
            else if(payload.t == "GROUP_MESSAGE_CREATE")
            {
                GroupEventData data = JsonConvert.DeserializeObject<GroupEventData>(payload.d.ToString());
            }
            else if(payload.t == "INTERACTION_CREATE")
            {
                InteractionEventData data = JsonConvert.DeserializeObject<InteractionEventData>(payload.d.ToString());
            }
        }
        
    }

    private async Task StartHeartBeatAsync(CancellationToken token)
    {
        while(!token.IsCancellationRequested)
        {
            Payload pay = new Payload() { op = (int)OpCode.Heartbeat, d = _lastS! };
            string json = JsonConvert.SerializeObject(pay);
            await _webSocket.SendAsync(json);
            await Task.Delay(_heartBeatInterval, token);
        }
        
    }

    public async Task ConnectAsync()
    {
        _ = _accessTokenManager.StartGetAccessToken(_options.AppId, _options.AppSecret, _source.Token);
        await _accessTokenManager.WaitForTokenAsync(_source.Token);
        string obj = await _httpHelper.GetAsync($"{Constants.BaseUrl}gateway",_accessTokenManager.AccessToken);
        JObject response = JObject.Parse(obj);
        await _webSocket.ConnectAsync(response["url"]!.Value<string>()!);
        _logger.LogInformation("连接成功");
    }

    public async Task LoginAsync()
    {
        Payload payload = new Payload() { op = 2, d = new 
        {
            token = $"QQBot {_accessTokenManager.AccessToken}",
            intents = Intents.GROUP_AND_C2C_EVENT | Intents.INTERACTION,
            shard = new int[] { 0,_options.Shards},
            properties = new { }
        } };
        string json = JsonConvert.SerializeObject(payload);
        await _webSocket.SendAsync(json);
    }

    public void Dispose()
    {
        _source.Cancel();
    }

    private async Task ReconnectAsync()
    {
        Payload pay = new Payload() { op = (int)OpCode.Resume, d = new 
        {
            token = $"QQBot {_accessTokenManager.AccessToken}",
            session_id = sessionId,
            seq = _lastS
        } };
        string json = JsonConvert.SerializeObject(pay);
        await _webSocket.SendAsync(json);
    }
}
