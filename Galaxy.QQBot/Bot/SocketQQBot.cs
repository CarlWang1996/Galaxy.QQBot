using Galaxy.QQBot.Common;
using Galaxy.QQBot.Common.EventData;
using Galaxy.QQBot.Identity;
using Galaxy.QQBot.Tool;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Galaxy.QQBot.Bot;

public class SocketQQBot : IDisposable
{
    private string appId = "102077650";
    private string appSecret = "5Oi2Ni4QnBZyNnDe5X0TxRwRxT0Y6fEo";
    private readonly IAccessTokenManager _accessTokenManager;
    private readonly IWebSocketClient _webSocket;
    private readonly IHttpHelper _httpHelper;
    private readonly ILogger _logger;
    private CancellationTokenSource _source = new CancellationTokenSource();
    private int? _lastS = null;
    private int _heartBeatInterval = 0;
    private string sessionId = string.Empty;
    private static readonly object sLock = new object();

    public SocketQQBot(
        IWebSocketClient webSocket, 
        IHttpHelper httpHelper,
        IAccessTokenManager accessTokenManager,
        ILogger<SocketQQBot> logger)
    {
        _webSocket = webSocket;
        _httpHelper = httpHelper;
        _accessTokenManager = accessTokenManager;
        _logger = logger;
        _webSocket.OnMessageReceived += HandleMessageAsync;
        _webSocket.OnDisconnected += HandleDisconnectedAsync;
    }

    private async Task HandleDisconnectedAsync(string arg)
    {
        _logger.LogWarning("WebSocket 断开: {msg}", arg);

        // 假设你的 WebSocketClient 内部在关闭时，能抛出带有状态码的异常或事件
        // 这里演示业务逻辑的分支处理：

        // 如果是因为 4014 (无权限) 或 4915 (封禁) 导致的断开，就不应该重连了
        if (arg.Contains("4914") || arg.Contains("4915") || arg.Contains("4013"))
        {
            _logger.LogError("机器人已下架或被封禁或 Intent 无效，停止重连。");
            return;
        }

        // 如果是因为 4009 (连接过期)，需要重连并发 Resume (OpCode 6)
        if (arg.Contains("4009"))
        {
            _logger.LogInformation("连接过期，尝试重连并 Resume...");
            await ReconnectAsync();
            return;
        }

        // 其他情况（如 4900 内部错误、网络波动），重连后重新 Identify (OpCode 2)
        _logger.LogInformation("尝试重连并重新登录...");
        await ConnectAsync();
        if (!string.IsNullOrEmpty(sessionId) && _lastS != null)
        {
            await ReconnectAsync();
        }
        else
        {
            await LoginAsync(); // 重新发送 Identify
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
            _logger.LogInformation("收到心跳回复");
        }
        else if(payload.op == (int)OpCode.Dispatch)
        {
            if (payload.t == "READY")
            {
                _ = StartHeartBeatAsync(_source.Token);
                try
                {
                    ReadyEventData readyData = JsonConvert.DeserializeObject<ReadyEventData>(payload.d.ToString());
                    sessionId = readyData.session_id;
                    _lastS = payload.s;
                    _logger.LogInformation("Bot 登录成功 (READY)，SessionId: {sid}", readyData.session_id);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "解析或推送 READY 事件失败");
                }
            }
            else if (payload.t == "RESUMED")
            {
                _lastS = payload.s;
                _logger.LogInformation("收到重连回复");
            }
            else if (payload.t == "C2C_MESSAGE_CREATE")
            {
                C2cEventData data = JsonConvert.DeserializeObject<C2cEventData>(payload.d.ToString());
            }
            else if(payload.t == "GROUP_AT_MESSAGE_CREATE")
            {
                GroupEventData data = JsonConvert.DeserializeObject<GroupEventData>(payload.d.ToString());
            }
            else if(payload.t == "GROUP_MESSAGE_CREATE")
            {
                GroupEventData data = JsonConvert.DeserializeObject<GroupEventData>(payload.d.ToString());
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
        _ = _accessTokenManager.StartGetAccessToken(appId, appSecret, _source.Token);
        await _accessTokenManager.WaitForTokenAsync(_source.Token);
        string obj = await _httpHelper.GetWithTokenAsync($"{Constants.BaseUrl}gateway",_accessTokenManager.AccessToken);
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
            shard = new int[] { 0,1},
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
