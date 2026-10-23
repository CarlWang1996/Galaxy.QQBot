using Galaxy.QQBot.Common;
using Galaxy.QQBot.Tool;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Galaxy.QQBot.Manager;


internal class AccessTokenManager
{
    private readonly HttpHelper _httpHelper;
    private readonly ILogger _logger;
    private readonly object _sync = new();
    private string _accessToken = string.Empty;
    private TaskCompletionSource<string> _tokenReadyTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);

    internal AccessTokenManager(HttpHelper httpHelper, ILoggerFactory factory)
    {
        _httpHelper = httpHelper;
        _logger = factory.CreateLogger<AccessTokenManager>();
    }

    public string AccessToken
    {
        get
        {
            lock (_sync)
            {
                return _accessToken;
            }
        }
    }

    public async Task<string> WaitForTokenAsync(CancellationToken ct = default)
    {
        while (true)
        {
            Task<string> task;
            lock (_sync)
            {
                if (!string.IsNullOrEmpty(_accessToken))
                {
                    return _accessToken;
                }

                task = _tokenReadyTcs.Task;
            }

            try
            {
                return await task.WaitAsync(ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "获取 AccessToken 失败，等待刷新循环重试");
            }
        }
    }

    public async Task StartGetAccessToken(string appId, string clientSecret, CancellationToken clt)
    {
        do
        {
            try
            {
                var result = await _httpHelper.PostAsyncWithOutToken($"{Constants.BaseUrl}app/getAppAccessToken", new { appId, clientSecret }, clt);
                using var doc = JsonDocument.Parse(result);
                var root = doc.RootElement;

                if (root.TryGetProperty("code", out var codeElement))
                {
                    int code = codeElement.GetInt32();
                    _logger.LogError("获取AccessToken失败,错误码:{code}", code);
                    NotifyCurrentWaiterFailed(new Exception($"获取AccessToken失败,错误码:{code}"));
                    await SafeDelayAsync(TimeSpan.FromSeconds(5), clt);
                    continue;
                }

                if (root.TryGetProperty("access_token", out var tokenElement) && root.TryGetProperty("expires_in", out var expiresElement))
                {
                    string token = tokenElement.GetString()!;
                    if (!TryGetExpiresIn(expiresElement, out int expiresIn))
                    {
                        _logger.LogWarning("expires_in 格式无法解析: {value}", expiresElement.GetRawText());
                        await SafeDelayAsync(TimeSpan.FromSeconds(5), clt);
                        continue;
                    }

                    PublishToken(token);
                    int delaySeconds = Math.Max(expiresIn - 30, 5);
                    await SafeDelayAsync(TimeSpan.FromSeconds(delaySeconds), clt);
                }
                else
                {
                    _logger.LogWarning("解析 Token 响应失败，缺失字段: {result}", result);
                    await SafeDelayAsync(TimeSpan.FromSeconds(5), clt);
                }
            }
            catch (OperationCanceledException) when (clt.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "刷新 Token 异常");
                NotifyCurrentWaiterFailed(ex);

                if (!await SafeDelayAsync(TimeSpan.FromSeconds(5), clt))
                {
                    break;
                }
            }
        } while (!clt.IsCancellationRequested);
    }

    private void PublishToken(string token)
    {
        TaskCompletionSource<string> toComplete;
        lock (_sync)
        {
            _accessToken = token;
            if (_tokenReadyTcs.Task.IsCompleted)
            {
                _tokenReadyTcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
            }

            toComplete = _tokenReadyTcs;
        }

        toComplete.TrySetResult(token);
        _logger.LogDebug("获取accessToken成功");
    }

    private void NotifyCurrentWaiterFailed(Exception ex)
    {
        TaskCompletionSource<string> oldTcs;
        lock (_sync)
        {
            oldTcs = _tokenReadyTcs;
            _tokenReadyTcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        }

        oldTcs.TrySetException(ex);
    }

    private static bool TryGetExpiresIn(JsonElement element, out int expiresIn)
    {
        expiresIn = 0;

        if (element.ValueKind == JsonValueKind.String)
        {
            return int.TryParse(element.GetString(), out expiresIn) && expiresIn > 0;
        }

        if (element.ValueKind == JsonValueKind.Number)
        {
            if (element.TryGetInt32(out expiresIn))
            {
                return expiresIn > 0;
            }

            if (element.TryGetDouble(out double seconds) && seconds > 0 && seconds <= int.MaxValue)
            {
                expiresIn = (int)seconds;
                return true;
            }
        }

        return false;
    }

    private static async Task<bool> SafeDelayAsync(TimeSpan delay, CancellationToken ct)
    {
        try
        {
            await Task.Delay(delay, ct);
            return true;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }
}