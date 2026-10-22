namespace Galaxy.QQBot.Tool;

/// <summary>
/// HTTP 请求工具接口
/// </summary>
public interface IHttpHelper
{
    Task<string> GetAsync(string url, string token, CancellationToken cancellationToken = default);
    Task<string> PostAsync<T>(string url, string token, T data, CancellationToken cancellationToken = default);
    Task<string> PutAsync<T>(string url, string token, T data, CancellationToken cancellationToken = default);
    Task<string> PostWithOutTokenAsync<T>(string url, T data, CancellationToken cancellationToken = default);
    Task<string> PutAsyncWithoutToken(string url, byte[] data, CancellationToken cancellationToken = default);
}
