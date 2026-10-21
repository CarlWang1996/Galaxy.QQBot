namespace Galaxy.QQBot.Tool;

/// <summary>
/// WebSocket 客户端工具接口
/// </summary>
public interface IWebSocketClient : IAsyncDisposable
{
    /// <summary>连接状态</summary>
    bool IsConnected { get; }

    /// <summary>收到文本消息的事件</summary>
    event Func<string, Task>? OnMessageReceived;

    /// <summary>连接断开的事件（参数：重连次数或异常信息）</summary>
    event Func<string, Task>? OnDisconnected;

    /// <summary>建立连接并启动后台接收循环</summary>
    Task ConnectAsync(string url, CancellationToken cancellationToken = default);

    /// <summary>发送文本消息</summary>
    Task SendAsync(string message, CancellationToken cancellationToken = default);

    /// <summary>发送二进制数据</summary>
    Task SendAsync(byte[] data, CancellationToken cancellationToken = default);

    /// <summary>主动断开连接</summary>
    Task DisconnectAsync();
}
