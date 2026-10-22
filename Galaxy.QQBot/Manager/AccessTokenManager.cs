using Galaxy.QQBot.Common;
using Galaxy.QQBot.Tool;
using Microsoft.Extensions.Logging;
using System.Text.Json;

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
                using var doc = JsonDocument.Parse(result);
                var root = doc.RootElement;
                if (root.TryGetProperty("code", out var codeElement))
                {
                    int code = codeElement.GetInt32();
                    _logger.LogError($"获取AccessToken失败,错误码:{code}");

                    var ex = new Exception($"获取AccessToken失败,错误码:{code}");
                    if (!_tokenReadyTcs.Task.IsCompleted)
                    {
                        _tokenReadyTcs.TrySetException(ex);
                    }
                    throw ex;
                }

                if (root.TryGetProperty("access_token", out var tokenElement) &&
                    root.TryGetProperty("expires_in", out var expiresElement))
                {
                    string token = tokenElement.GetString()!;
                    expiresIn = int.Parse(expiresElement.GetString()!);

                    lock (tokenLock)
                    {
                        _accessToken = token;
                        _tokenReadyTcs.TrySetResult(_accessToken);
                        _logger.LogInformation("获取accessToken成功");
                    }

                    int delaySeconds = Math.Max(expiresIn - 30, 5);
                    await Task.Delay(delaySeconds * 1000, clt);
                }
                else
                {
                    _logger.LogWarning("解析 Token 响应失败，缺失字段: {result}", result);
                    await Task.Delay(5000, clt);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "刷新 Token 异常");

                try
                {
                    await Task.Delay(5000, clt);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        } while (!clt.IsCancellationRequested);
    }
}