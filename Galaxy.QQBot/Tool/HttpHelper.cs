using System;
using System.Collections.Generic;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace Galaxy.QQBot.Tool;

/// <summary>
/// 基于 IHttpClientFactory 的 HTTP 请求工具实现
/// </summary>
public class HttpHelper : IHttpHelper
{
    private readonly HttpClient _httpClient;
    private readonly JsonSerializerOptions _jsonOptions;

    private static readonly JsonSerializerOptions DefaultJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    /// <summary>
    /// 构造函数注入 HttpClient。
    /// 若使用 AddHttpClient 注册，可注入命名客户端；否则注入默认 HttpClient。
    /// </summary>
    public HttpHelper(HttpClient httpClient)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _jsonOptions = DefaultJsonOptions;
    }

    public async Task<string> GetStringAsync(string url, CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.GetAsync(url, cancellationToken).ConfigureAwait(false);
        await EnsureSuccessAsync(response).ConfigureAwait(false);
        return await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<T?> GetAsync<T>(string url, CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.GetAsync(url, cancellationToken).ConfigureAwait(false);
        await EnsureSuccessAsync(response).ConfigureAwait(false);
        return await response.Content.ReadFromJsonAsync<T>(_jsonOptions, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<TResponse?> PostAsync<TRequest, TResponse>(
        string url,
        TRequest data,
        CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient
            .PostAsJsonAsync(url, data, _jsonOptions, cancellationToken)
            .ConfigureAwait(false);
        await EnsureSuccessAsync(response).ConfigureAwait(false);
        return await response.Content.ReadFromJsonAsync<TResponse>(_jsonOptions, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<string> PostStringAsync<TRequest>(
        string url,
        TRequest data,
        CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient
            .PostAsJsonAsync(url, data, _jsonOptions, cancellationToken)
            .ConfigureAwait(false);
        await EnsureSuccessAsync(response).ConfigureAwait(false);
        return await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<TResponse?> PutAsync<TRequest, TResponse>(
        string url,
        TRequest data,
        CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient
            .PutAsJsonAsync(url, data, _jsonOptions, cancellationToken)
            .ConfigureAwait(false);
        await EnsureSuccessAsync(response).ConfigureAwait(false);
        return await response.Content.ReadFromJsonAsync<TResponse>(_jsonOptions, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<bool> DeleteAsync(string url, CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.DeleteAsync(url, cancellationToken).ConfigureAwait(false);
        await EnsureSuccessAsync(response).ConfigureAwait(false);
        return response.IsSuccessStatusCode;
    }

    public async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        await EnsureSuccessAsync(response).ConfigureAwait(false);
        return response;
    }

    public async Task<string> GetWithTokenAsync(string url, string token, CancellationToken cancellationToken = default)
    {
        HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add("Authorization", $"QQBot {token}");
        var response = await SendAsync(request, cancellationToken);
        return await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// 统一错误处理：非成功状态码抛出带响应体的异常，方便排查问题
    /// </summary>
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
}
