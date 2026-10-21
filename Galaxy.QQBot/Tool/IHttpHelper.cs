namespace Galaxy.QQBot.Tool;

/// <summary>
/// HTTP 请求工具接口
/// </summary>
public interface IHttpHelper
{
    /// <summary>发送 GET 请求并返回字符串</summary>
    Task<string> GetStringAsync(string url, CancellationToken cancellationToken = default);

    /// <summary>发送 GET 请求并反序列化为对象</summary>
    Task<T?> GetAsync<T>(string url, CancellationToken cancellationToken = default);

    /// <summary>发送 POST 请求（JSON body）</summary>
    Task<TResponse?> PostAsync<TRequest, TResponse>(
        string url,
        TRequest data,
        CancellationToken cancellationToken = default);

    /// <summary>发送 POST 请求，返回字符串</summary>
    Task<string> PostStringAsync<TRequest>(
        string url,
        TRequest data,
        CancellationToken cancellationToken = default);

    /// <summary>发送 PUT 请求</summary>
    Task<TResponse?> PutAsync<TRequest, TResponse>(
        string url,
        TRequest data,
        CancellationToken cancellationToken = default);

    /// <summary>发送 DELETE 请求</summary>
    Task<bool> DeleteAsync(string url, CancellationToken cancellationToken = default);

    /// <summary>发送原始请求（可自定义 Method / Header / Body）</summary>
    Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken = default);

    Task<string> GetWithTokenAsync(string url, string token, CancellationToken cancellationToken = default);
}
