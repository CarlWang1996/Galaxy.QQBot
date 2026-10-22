using Galaxy.QQBot.Common;
using Galaxy.QQBot.Tool;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;

namespace Galaxy.QQBot.Manager;

public interface IAccessTokenManager
{
    string AccessToken { get; }
    Task<string> WaitForTokenAsync(CancellationToken ct = default);
    Task StartGetAccessToken(string appId, string clientSecret, CancellationToken clt);
}

public class AccessTokenManager : IAccessTokenManager
{
    private readonly IHttpHelper _httpHelper;
    private readonly ILogger _logger;
    private string _accessToken = string.Empty;
    private static readonly object tokenLock = new object();
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
        if (!string.IsNullOrEmpty(AccessToken))
        {
            return Task.FromResult(AccessToken);
        }

        return _tokenReadyTcs.Task.WaitAsync(ct);
    }

    public async Task StartGetAccessToken(string appId, string clientSecret, CancellationToken clt)
    {
        do
        {
            int expiresIn = 0;
            try
            {
                var result = await _httpHelper.PostAsyncWithOutToken($"{Constants.BaseUrl}app/getAppAccessToken", new { appId, clientSecret });
                JObject response = JObject.Parse(result);
                if (response.TryGetValue("code",out JToken? code))
                {
                    _logger.LogError($"获取AccessToken失败,错误码:{code!.Value<int>()}");
                    if (!_tokenReadyTcs.Task.IsCompleted)
                    {
                        _tokenReadyTcs.TrySetException(new Exception($"获取AccessToken失败,错误码:{code}"));
                    }

                    throw new Exception($"获取AccessToken失败,错误码:{code}");
                }

                if (response.TryGetValue("access_token", out JToken? token))
                {
                    lock (tokenLock)
                    {
                        expiresIn = response["expires_in"]!.Value<int>();
                        _accessToken = token.Value<string>()!;
                        _tokenReadyTcs.TrySetResult(_accessToken);
                        _logger.LogInformation("获取accessToken成功:{0}", AccessToken);
                    }
                }

                await Task.Delay((expiresIn - 30) * 1000, clt);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "刷新 Token 异常");
                await Task.Delay(5000, clt);
            }
        } while (!clt.IsCancellationRequested);
    }
}