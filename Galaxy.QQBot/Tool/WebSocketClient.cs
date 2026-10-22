using System.Net.WebSockets;
using System.Text;

namespace Galaxy.QQBot.Tool;

public interface IWebSocketClient : IAsyncDisposable
{
    bool IsConnected { get; }

    event Func<string, Task>? OnMessageReceived;

    event Func<string, Task>? OnDisconnected;

    Task ConnectAsync(string url, CancellationToken cancellationToken = default);

    Task SendAsync(string message, CancellationToken cancellationToken = default);

    Task SendAsync(byte[] data, CancellationToken cancellationToken = default);

    Task DisconnectAsync();
}

/// <summary>
/// 纯 WebSocket 客户端：只负责“连接 + 收发”，不做任何重连决策。
/// 重连策略统一由上层（SocketQQBot）控制。
/// </summary>
public class WebSocketClient : IWebSocketClient
{
    private ClientWebSocket? _webSocket;
    private readonly SemaphoreSlim _sendLock = new(1, 1);
    private CancellationTokenSource? _cts;
    private Task? _receiveTask;
    private string _url = string.Empty;
    private readonly int _bufferSize = 1024 * 4;
    private int _disconnectNotified = 0;
    public bool IsConnected => _webSocket?.State == WebSocketState.Open;
    public event Func<string, Task>? OnMessageReceived;
    public event Func<string, Task>? OnDisconnected;

    public async Task ConnectAsync(string url, CancellationToken cancellationToken = default)
    {
        if (IsConnected)
        {
            return;
        }

        _url = url;
        _cts?.Dispose();
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        Interlocked.Exchange(ref _disconnectNotified, 0);
        await ConnectInternalAsync(_cts.Token);

        _receiveTask = Task.Run(() => ReceiveLoopAsync(_cts.Token), _cts.Token);
    }

    private async Task ConnectInternalAsync(CancellationToken token)
    {
        _webSocket?.Dispose();
        _webSocket = new ClientWebSocket();
        await _webSocket.ConnectAsync(new Uri(_url), token);
    }

    public async Task SendAsync(string message, CancellationToken cancellationToken = default)
    {
        if (!IsConnected)
        {
            throw new InvalidOperationException("WebSocket 未连接。");
        }

        var bytes = Encoding.UTF8.GetBytes(message);
        await SendAsync(bytes, WebSocketMessageType.Text, cancellationToken);
    }

    public async Task SendAsync(byte[] data, CancellationToken cancellationToken = default)
    {
        if (!IsConnected)
        {
            throw new InvalidOperationException("WebSocket 未连接。");
        }

        await SendAsync(data, WebSocketMessageType.Binary, cancellationToken);
    }

    private async Task SendAsync(byte[] data, WebSocketMessageType messageType, CancellationToken cancellationToken)
    {
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
        try
        {
            while (!token.IsCancellationRequested && _webSocket?.State == WebSocketState.Open)
            {
                using var ms = new MemoryStream();
                WebSocketReceiveResult result;
                do
                {
                    result = await _webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), token);

                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        await NotifyDisconnectedOnceAsync(
                            $"连接关闭: Code: {(int)result.CloseStatus!}, Reason: {result.CloseStatusDescription}");
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
            }
        }
        catch (OperationCanceledException)
        {
            
        }
        catch (Exception ex)
        {
            await NotifyDisconnectedOnceAsync($"接收异常: {ex.Message}");
        }
    }

    private async Task NotifyDisconnectedOnceAsync(string reason)
    {
        if (Interlocked.Exchange(ref _disconnectNotified, 1) == 1)
        {
            return;
        }

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
                await _webSocket.CloseAsync(
                    WebSocketCloseStatus.NormalClosure, "Client closed", CancellationToken.None);
            }
            catch
            {
            }
        }

        _webSocket?.Dispose();
        _webSocket = null;

        if (_receiveTask != null)
        {
            try { await _receiveTask; } catch { }
            _receiveTask = null;
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