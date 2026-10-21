using System.Net.WebSockets;
using System.Text;

namespace Galaxy.QQBot.Tool;

public class WebSocketClient : IWebSocketClient
{
    private ClientWebSocket? _webSocket;
    private readonly SemaphoreSlim _sendLock = new(1, 1);
    private CancellationTokenSource? _cts;
    private Task? _receiveTask;

    private string _url = string.Empty;
    private readonly int _bufferSize = 1024 * 4; // 4KB 缓冲区
    private readonly TimeSpan _heartbeatInterval = TimeSpan.FromSeconds(30);

    public bool IsConnected => _webSocket?.State == WebSocketState.Open;

    public event Func<string, Task>? OnMessageReceived;
    public event Func<string, Task>? OnDisconnected;

    public async Task ConnectAsync(string url, CancellationToken cancellationToken = default)
    {
        if (IsConnected) return;

        _url = url;
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        await ConnectInternalAsync(_cts.Token);

        // 启动后台接收任务
        _receiveTask = Task.Run(() => ReceiveLoopAsync(_cts.Token), _cts.Token);
    }

    private async Task ConnectInternalAsync(CancellationToken token)
    {
        _webSocket?.Dispose();
        _webSocket = new ClientWebSocket();

        // 可在此处配置 Headers、Cookies 或代理
        // _webSocket.Options.SetRequestHeader("Authorization", "Bearer xxx");
        // _webSocket.Options.KeepAliveInterval = TimeSpan.FromSeconds(20); // 内置心跳

        try
        {
            await _webSocket.ConnectAsync(new Uri(_url), token);
            _ = Task.Run(() => HeartbeatLoopAsync(token), token);
        }
        catch (Exception ex)
        {
            await TriggerDisconnectedAsync($"连接失败: {ex.Message}");
            throw;
        }
    }

    public async Task SendAsync(string message, CancellationToken cancellationToken = default)
    {
        if (!IsConnected)
            throw new InvalidOperationException("WebSocket 未连接。");

        var bytes = Encoding.UTF8.GetBytes(message);
        await SendAsync(bytes, WebSocketMessageType.Text, cancellationToken);
    }

    public async Task SendAsync(byte[] data, CancellationToken cancellationToken = default)
    {
        if (!IsConnected)
            throw new InvalidOperationException("WebSocket 未连接。");

        await SendAsync(data, WebSocketMessageType.Binary, cancellationToken);
    }

    private async Task SendAsync(byte[] data, WebSocketMessageType messageType, CancellationToken cancellationToken)
    {
        // 必须加锁：ClientWebSocket 不允许并发发送
        await _sendLock.WaitAsync(cancellationToken);
        try
        {
            if (_webSocket?.State == WebSocketState.Open)
            {
                await _webSocket.SendAsync(
                    new ArraySegment<byte>(data),
                    messageType,
                    endOfMessage: true,
                    cancellationToken);
            }
        }
        finally
        {
            _sendLock.Release();
        }
    }

    private async Task ReceiveLoopAsync(CancellationToken token)
    {
        var buffer = new byte[_bufferSize];

        while (!token.IsCancellationRequested && _webSocket?.State == WebSocketState.Open)
        {
            try
            {
                using var ms = new MemoryStream();
                WebSocketReceiveResult result;

                // 循环接收直到 EndOfMessage，解决分包/分片问题
                do
                {
                    result = await _webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), token);

                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        await HandleCloseAsync(result.CloseStatusDescription ?? "服务器主动关闭");
                        return;
                    }

                    ms.Write(buffer, 0, result.Count);
                }
                while (!result.EndOfMessage);

                if (result.MessageType == WebSocketMessageType.Text)
                {
                    var message = Encoding.UTF8.GetString(ms.ToArray());
                    if (OnMessageReceived != null)
                    {
                        await OnMessageReceived.Invoke(message);
                    }
                }
                else if (result.MessageType == WebSocketMessageType.Close)
                {
                    // result.CloseStatus 是 WebSocketCloseStatus 枚举（如 NormalClosure, EndpointUnavailable 等）
                    // 但 QQ 的 4000+ 错误码实际上是在 CloseStatusDescription 中返回的，或者会直接发一个 Close 帧
                    var closeCode = (int)result.CloseStatus!;
                    var description = result.CloseStatusDescription;

                    // 你可以将 (closeCode, description) 通过 OnDisconnected 事件抛给 SocketQQBot
                    await HandleCloseAsync($"Code: {closeCode}, Reason: {description}");
                    return;
                }
            }
            catch (OperationCanceledException)
            {
                break; // 正常取消
            }
            catch (Exception ex)
            {
                await TriggerDisconnectedAsync($"接收异常: {ex.Message}");
                await AttemptReconnectAsync(token);
                break; // 退出当前循环，重连成功后会启动新的 ReceiveLoop
            }
        }
    }

    private async Task HandleCloseAsync(string reason)
    {
        await TriggerDisconnectedAsync($"连接关闭: {reason}");

        if (_webSocket?.State == WebSocketState.CloseReceived)
        {
            try
            {
                await _webSocket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None);
            }
            catch { /* 忽略关闭时的异常 */ }
        }

        await AttemptReconnectAsync(_cts?.Token ?? CancellationToken.None);
    }

    private async Task HeartbeatLoopAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested && IsConnected)
        {
            try
            {
                await Task.Delay(_heartbeatInterval, token);
                if (IsConnected)
                {
                    // 发送 Ping 帧保活
                    await _webSocket!.SendAsync(
                        new ArraySegment<byte>(Array.Empty<byte>()),
                        WebSocketMessageType.Binary,
                        true,
                        token);
                }
            }
            catch (OperationCanceledException) { break; }
            catch { break; /* 心跳失败，由接收循环去处理异常 */ }
        }
    }

    private async Task AttemptReconnectAsync(CancellationToken token)
    {
        if (token.IsCancellationRequested) return;

        var retryCount = 0;
        while (!token.IsCancellationRequested && !IsConnected)
        {
            retryCount++;
            var delay = TimeSpan.FromSeconds(Math.Min(30, Math.Pow(2, retryCount))); // 指数退避
            await Task.Delay(delay, token);

            try
            {
                await ConnectInternalAsync(token);
                // 重连成功后重新启动接收
                _receiveTask = Task.Run(() => ReceiveLoopAsync(token), token);
                break;
            }
            catch
            {
                // 重连失败，继续下一次循环
            }
        }
    }

    private async Task TriggerDisconnectedAsync(string reason)
    {
        if (OnDisconnected != null)
        {
            await OnDisconnected.Invoke(reason);
        }
    }

    public async Task DisconnectAsync()
    {
        _cts?.Cancel();

        if (_webSocket?.State == WebSocketState.Open)
        {
            try
            {
                await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Client closed", CancellationToken.None);
            }
            catch { /* 忽略 */ }
        }

        _webSocket?.Dispose();
        _webSocket = null;

        if (_receiveTask != null)
        {
            try { await _receiveTask; } catch { /* 忽略 */ }
        }
    }

    public async ValueTask DisposeAsync()
    {
        await DisconnectAsync();
        _cts?.Dispose();
        _sendLock.Dispose();
        GC.SuppressFinalize(this);
    }
}
