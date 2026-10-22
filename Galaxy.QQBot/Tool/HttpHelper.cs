using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace Galaxy.QQBot.Tool;

public class HttpHelper : IDisposable
{
    private readonly SocketsHttpHandler _httpClientHandler;

    public HttpHelper()
    {
        _httpClientHandler = new SocketsHttpHandler
        {
            PooledConnectionLifetime = TimeSpan.FromMinutes(2),
            PooledConnectionIdleTimeout = TimeSpan.FromMinutes(1),
            MaxConnectionsPerServer = 16
        };
    }

    private async Task<string> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken = default)
    {
        using var httpClient = new HttpClient(_httpClientHandler, false);
        using var response = await httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        await EnsureSuccessAsync(response).ConfigureAwait(false);
        string ret = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        return ret;
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var body = string.Empty;
        try
        {
            body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        }
        catch
        {
        }

        throw new HttpRequestException(
            $"HTTP 请求失败: {(int)response.StatusCode} {response.ReasonPhrase}. " +
            $"Url: {response.RequestMessage?.RequestUri}, Body: {body}",
            inner: null,
            statusCode: response.StatusCode);
    }

    public async Task<string> GetAsync(string url, string token, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add("Authorization", $"QQBot {token}");
        return await SendAsync(request, cancellationToken);
    }

    public async Task<string> PostAsync<T>(string url, string token, T data, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Headers.Add("Authorization", $"QQBot {token}");
        request.Content = JsonContent.Create(data);
        return await SendAsync(request, cancellationToken);
    }

    public async Task<string> PostAsyncWithOutToken<T>(string url, T data, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Content = JsonContent.Create(data);
        return await SendAsync(request, cancellationToken);
    }

    public async Task<string> PutAsync<T>(string url, string token, T data, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Put, url);
        request.Headers.Add("Authorization", $"QQBot {token}");
        request.Content = JsonContent.Create(data);
        return await SendAsync(request, cancellationToken);
    }

    public async Task<string> PutAsyncWithoutToken(string url, byte[] data, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Put, url);
        request.Content = new ByteArrayContent(data);
        request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        return await SendAsync(request, cancellationToken);
    }

    public void Dispose()
    {
        _httpClientHandler.Dispose();
    }
}
