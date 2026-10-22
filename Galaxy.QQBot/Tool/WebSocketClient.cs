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

public class WebSocketClient : IWebSocketClient
{
    private ClientWebSocket? _webSocket;
    private readonly SemaphoreSlim _sendLock = new(1, 1);
    private CancellationTokenSource? _cts;
    private Task? _receiveTask;
    private string _url = string.Empty;
    private readonly int _bufferSize = 1024 * 4;
    private readonly TimeSpan _heartbeatInterval = TimeSpan.FromSeconds(30);
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
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        await ConnectInternalAsync(_cts.Token);
        _receiveTask = Task.Run(() => ReceiveLoopAsync(_cts.Token), _cts.Token);
    }

    private async Task ConnectInternalAsync(CancellationToken token)
    {
        _webSocket?.Dispose();
        _webSocket = new ClientWebSocket();
        try
        {
            await _webSocket.ConnectAsync(new Uri(_url), token);
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
        while (!token.IsCancellationRequested && _webSocket?.State == WebSocketState.Open)
        {
            try
            {
                using var ms = new MemoryStream();
                WebSocketReceiveResult result;
                do
                {
                    result = await _webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), token);

                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        await HandleCloseAsync(result.CloseStatusDescription ?? "服务器主动关闭");
                        return;
                    }

                    ms.Write(buffer, 0, result.Count);
                }while (!result.EndOfMessage);

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
                    var closeCode = (int)result.CloseStatus!;
                    var description = result.CloseStatusDescription;
                    await HandleCloseAsync($"Code: {closeCode}, Reason: {description}");
                    return;
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                await TriggerDisconnectedAsync($"接收异常: {ex.Message}");
                await AttemptReconnectAsync(token);
                break;
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
            catch 
            {
            }
        }

        await AttemptReconnectAsync(_cts?.Token ?? CancellationToken.None);
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
                _receiveTask = Task.Run(() => ReceiveLoopAsync(token), token);
                break;
            }
            catch
            {
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
            catch 
            {
            }
        }

        _webSocket?.Dispose();
        _webSocket = null;
        if (_receiveTask != null)
        {
            try { await _receiveTask; } catch { }
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
