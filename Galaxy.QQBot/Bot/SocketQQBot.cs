using Galaxy.QQBot.Common;
using Galaxy.QQBot.Manager;
using Galaxy.QQBot.Tool;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Galaxy.QQBot.Bot;

public class SocketQQBot : IAsyncDisposable
{
    internal record GatewayBotResponse(string url, int shards, SessionStartLimit? session_start_limit);
    internal record SessionStartLimit(int total, int remaining, long reset_after, int max_concurrency);
    private readonly string _appId;
    private readonly string _appSecret;
    private readonly ILogger _logger;
    private readonly ILoggerFactory _loggerFactory;
    private readonly HttpHelper _httpHelper;
    private readonly AccessTokenManager _accessTokenManager;
    private readonly BotMessageManager _botMessageManager;
    private readonly CancellationTokenSource _source = new();
    private readonly List<ShardConnection> _shards = new();
    private readonly object _tokenLock = new();
    private Task? _tokenRefreshTask;
    private CancellationTokenSource? _tokenRefreshCts;
    public BotMessageManager Messages => _botMessageManager;

    public SocketQQBot(string appId, string appSecret, ILoggerFactory? factory = null)
    {
        _appId = appId;
        _appSecret = appSecret;
        _loggerFactory = factory ?? new NullLoggerFactory();
        _logger = _loggerFactory.CreateLogger<SocketQQBot>();
        _httpHelper = new HttpHelper();
        _accessTokenManager = new AccessTokenManager(_httpHelper, _loggerFactory);
        _botMessageManager = new BotMessageManager(_httpHelper, _accessTokenManager);

        if (string.IsNullOrEmpty(_appId) || string.IsNullOrEmpty(_appSecret))
        {
            throw new InvalidOperationException("QQBot 的 AppId 或 AppSecret 未在配置中设置。");
        }
    }

    public async Task StartAsync(CancellationToken ct = default)
    {
        EnsureTokenRefreshLoopStarted();
        await _accessTokenManager.WaitForTokenAsync(_source.Token);
        string obj = await _httpHelper.GetAsync($"{Constants.BaseUrl}gateway/bot", _accessTokenManager.AccessToken);
        var gateway = JsonHelper.DeserializeElement<GatewayBotResponse>(obj)!;
        int total = Math.Max(1, gateway.shards);
        if (gateway.session_start_limit is { } limit && limit.remaining < total)
        {
            _logger.LogError("剩余连接数不足：需要 {need}，剩余 {remaining}（reset_after={reset}ms）", total, limit.remaining, limit.reset_after);
            throw new InvalidOperationException($"剩余连接数不足：需要 {total}，剩余 {limit.remaining}。");
        }

        for (int i = 0; i < total; i++)
        {
            var shard = new ShardConnection( i, total, _loggerFactory, _httpHelper, _accessTokenManager, _source.Token);
            _shards.Add(shard);
            try
            {
                await shard.StartAsync();
                _logger.LogInformation("shard {id}/{total} 已发起 Identify", i, total);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "shard {id}/{total} 启动失败", i, total);
            }
        }

        _logger.LogInformation("Bot 已启动，shards={shards}）", total);
    }

    private void EnsureTokenRefreshLoopStarted()
    {
        lock (_tokenLock)
        {
            if (_tokenRefreshTask != null && !_tokenRefreshTask.IsCompleted)
            {
                return;
            }

            _tokenRefreshCts?.Dispose();
            _tokenRefreshCts = CancellationTokenSource.CreateLinkedTokenSource(_source.Token);
            var token = _tokenRefreshCts.Token;
            _tokenRefreshTask = Task.Run(async () =>
            {
                try { await _accessTokenManager.StartGetAccessToken(_appId, _appSecret, token); }
                catch (OperationCanceledException) { }
                catch (Exception ex) { _logger.LogError(ex, "Token 刷新循环异常退出"); }
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
            try 
            { 
                _tokenRefreshCts.Cancel(); 
            }
            catch 
            {
            }
            _tokenRefreshCts.Dispose();
            _tokenRefreshCts = null;
            _tokenRefreshTask = null;
            _logger.LogInformation("已停止 Token 刷新循环。");
        }
    }

    public async ValueTask DisposeAsync()
    {
        _source.Cancel();
        StopTokenRefreshLoop();

        foreach (var shard in _shards)
        {
            try { await shard.DisposeAsync(); } catch { }
        }
        _shards.Clear();
        _httpHelper.Dispose();
        _source.Dispose();
    }
}