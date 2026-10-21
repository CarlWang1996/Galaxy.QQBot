using Galaxy.QQBot.Common;
using Galaxy.QQBot.Tool;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;

namespace Galaxy.QQBot.Identity;

public interface IAccessTokenManager
{
    string AccessToken { get; }

    /// <summary>
    /// 等待首次获取 Token 完成（异步阻塞，不卡线程）
    /// </summary>
    Task<string> WaitForTokenAsync(CancellationToken ct = default);

    Task StartGetAccessToken(string appId, string clientSecret, CancellationToken clt);
}

public class AccessTokenManager : IAccessTokenManager
{
    private readonly IHttpHelper _httpHelper;
    private readonly ILogger _logger;
    private string _accessToken = string.Empty;
    private static readonly object tokenLock = new object();

    // 核心：用于通知外部“Token 已就绪”
    private readonly TaskCompletionSource<string> _tokenReadyTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public AccessTokenManager(IHttpHelper httpHelper, ILogger<AccessTokenManager> logger)
    {
        _httpHelper = httpHelper;
        _logger = logger;
    }

    public string AccessToken
    {
        get
        {
            lock (tokenLock)
            {
                return _accessToken;
            }
        }
    }

    public Task<string> WaitForTokenAsync(CancellationToken ct = default)
    {
        // 如果已经有 Token 了，直接返回
        if (!string.IsNullOrEmpty(AccessToken))
            return Task.FromResult(AccessToken);

        // 否则等待 TCS 被设置
        return _tokenReadyTcs.Task.WaitAsync(ct);
    }

    public async Task StartGetAccessToken(string appId, string clientSecret, CancellationToken clt)
    {
        do
        {
            int expiresIn = 0;
            try
            {
                var result = await _httpHelper.PostStringAsync($"{Constants.BaseUrl}app/getAppAccessToken", new { appId, clientSecret });
                JObject response = JObject.Parse(result);
                if (response.ContainsKey("code"))
                {
                    int code = response["code"]!.Value<int>();
                    _logger.LogError($"获取AccessToken失败,错误码:{code}");
                    // 如果首次获取就失败，应该让等待方抛出异常，避免卡死
                    if (!_tokenReadyTcs.Task.IsCompleted)
                        _tokenReadyTcs.TrySetException(new Exception($"获取AccessToken失败,错误码:{code}"));

                    throw new Exception($"获取AccessToken失败,错误码:{code}");
                }

                if (response.TryGetValue("access_token", out JToken? token))
                {
                    lock (tokenLock)
                    {
                        expiresIn = response["expires_in"]!.Value<int>();
                        _accessToken = token.Value<string>()!;
                        _logger.LogInformation("获取accessToken成功:{0}", AccessToken);

                        // 核心：首次获取成功后，通知所有等待者
                        _tokenReadyTcs.TrySetResult(_accessToken);
                    }
                }

                // 提前 30 秒刷新
                await Task.Delay((expiresIn - 30) * 1000, clt);
            }
            catch (OperationCanceledException)
            {
                break; // 正常取消
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "刷新 Token 异常");
                // 如果出错了，等待一小会儿再重试，避免死循环疯狂请求
                await Task.Delay(5000, clt);
            }
        } while (!clt.IsCancellationRequested);
    }
}