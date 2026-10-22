using System.Net.Http.Json;

namespace Galaxy.QQBot.Tool;
public class HttpHelper : IHttpHelper
{
    private readonly HttpClient _httpClient;

    public HttpHelper(HttpClient httpClient)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
    }

    private async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        await EnsureSuccessAsync(response).ConfigureAwait(false);
        return response;
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode) return;

        var body = string.Empty;
        try
        {
            body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        }
        catch
        {
            // 忽略读取响应体异常
        }

        throw new HttpRequestException(
            $"HTTP 请求失败: {(int)response.StatusCode} {response.ReasonPhrase}. " +
            $"Url: {response.RequestMessage?.RequestUri}, Body: {body}",
            inner: null,
            statusCode: response.StatusCode);
    }

    public async Task<string> GetAsync(string url, string token, CancellationToken cancellationToken = default)
    {
        HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add("Authorization", $"QQBot {token}");
        var response = await SendAsync(request, cancellationToken);
        return await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<string> PostAsync<T>(string url, string token, T data, CancellationToken cancellationToken = default)
    {
        HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Headers.Add("Authorization", $"QQBot {token}");
        request.Content = JsonContent.Create(data);
        var response = await SendAsync(request, cancellationToken);
        return await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<string> PostWithOutTokenAsync<T>(string url, T data, CancellationToken cancellationToken = default)
    {
        HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Content = JsonContent.Create(data);
        var response = await SendAsync(request, cancellationToken);
        return await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<string> PutAsync<T>(string url, string token, T data, CancellationToken cancellationToken = default)
    {
        HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Put, url);
        request.Headers.Add("Authorization", $"QQBot {token}");
        request.Content = JsonContent.Create(data);
        var response = await SendAsync(request, cancellationToken);
        return await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<string> PutAsyncWithoutToken(string url, byte[] data, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Put, url);

        // 使用 ByteArrayContent 将 byte[] 包装成纯二进制内容
        request.Content = new ByteArrayContent(data);

        // 显式指定 Content-Type 为二进制流（有些云存储严格要求）
        request.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");

        var response = await _httpClient.SendAsync(request, cancellationToken);

        // 确保检查一下 HTTP 状态码，如果不是 200 会抛异常或记录日志
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
    }
}
