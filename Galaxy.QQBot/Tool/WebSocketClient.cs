using System.Net.WebSockets;
using System.Text;

namespace Galaxy.QQBot.Tool;

internal class WebSocketClient
{
    private ClientWebSocket? _webSocket;
    private readonly SemaphoreSlim _sendLock = new(1, 1);
    private CancellationTokenSource? _cts;
    private Task? _receiveTask;
    private string _url = string.Empty;
    private readonly int _bufferSize = 1024 * 4;
    private int _disconnectNotified = 0;
    public Action<Exception>? OnHandlerError { get; set; }
    public bool IsConnected => _webSocket?.State == WebSocketState.Open;
    public event Func<string, Task>? OnMessageReceived;
    public event Func<string, Task>? OnDisconnected;

    public async Task ConnectAsync(string url, CancellationToken cancellationToken = default)
    {
        if (IsConnected)
        {
            return;
        }

        _cts?.Cancel();
        await StopReceiveLoopAsync();
        _url = url;
        _cts?.Dispose();
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        Interlocked.Exchange(ref _disconnectNotified, 0);
        await ConnectInternalAsync(_cts.Token);
        var socket = _webSocket!;
        var token = _cts.Token;
        _receiveTask = Task.Run(() => ReceiveLoopAsync(socket, token), token);
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
                await _webSocket.SendAsync(new ArraySegment<byte>(data), messageType, endOfMessage: true, cancellationToken);
            }
        }
        finally
        {
            _sendLock.Release();
        }
    }

    private async Task ReceiveLoopAsync(ClientWebSocket socket, CancellationToken token)
    {
        var buffer = new byte[_bufferSize];
        try
        {
            while (!token.IsCancellationRequested && socket.State == WebSocketState.Open)
            {
                using var ms = new MemoryStream();
                WebSocketReceiveResult result;
                do
                {
                    result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), token);

                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        if (socket.State == WebSocketState.CloseReceived)
                        {
                            try
                            {
                                using var closeCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                                await socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "Client closed", closeCts.Token);
                            }
                            catch (Exception ex)
                            {
                                OnHandlerError?.Invoke(ex);
                            }
                        }

                        await NotifyDisconnectedOnceAsync($"连接关闭: Code: {(int)result.CloseStatus!}, Reason: {result.CloseStatusDescription}");
                        return;
                    }

                    ms.Write(buffer, 0, result.Count);
                }
                while (!result.EndOfMessage);

                if (result.MessageType == WebSocketMessageType.Text)
                {
                    var message = Encoding.UTF8.GetString(ms.ToArray());
                    await SafeInvokeMessageHandlersAsync(message);
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (WebSocketException ex)
        {
            await NotifyDisconnectedOnceAsync($"接收异常: {ex.Message}");
        }
        catch (Exception ex)
        {
            if (ReferenceEquals(_webSocket, socket))
            {
                await NotifyDisconnectedOnceAsync($"接收异常: {ex.Message}");
            }
        }
    }

    private async Task SafeInvokeMessageHandlersAsync(string message)
    {
        var handler = OnMessageReceived;
        if (handler == null)
        {
            return;
        }

        foreach (var d in handler.GetInvocationList())
        {
            try
            {
                await ((Func<string, Task>)d).Invoke(message);
            }
            catch (Exception ex)
            {
                OnHandlerError?.Invoke(ex);
            }
        }
    }

    private async Task NotifyDisconnectedOnceAsync(string reason)
    {
        if (Interlocked.Exchange(ref _disconnectNotified, 1) == 1)
        {
            return;
        }

        var handler = OnDisconnected;
        if (handler != null)
        {
            try
            {
                await handler.Invoke(reason);
            }
            catch (Exception ex)
            {
                OnHandlerError?.Invoke(ex);
            }
        }
    }

    private async Task StopReceiveLoopAsync()
    {
        if (_receiveTask == null)
        {
            return;
        }

        try
        {
            await _receiveTask.WaitAsync(TimeSpan.FromSeconds(5));
        }
        catch (TimeoutException ex)
        {
            OnHandlerError?.Invoke(ex);
        }
        catch
        {
        }
        finally
        {
            _receiveTask = null;
        }
    }

    public async Task DisconnectAsync()
    {
        _cts?.Cancel();
        var socket = _webSocket;

        if (socket != null)
        {
            try
            {
                if (socket.State == WebSocketState.Open)
                {
                    await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Client closed", CancellationToken.None);
                }
                else if (socket.State == WebSocketState.CloseReceived)
                {
                    await socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "Client closed", CancellationToken.None);
                }
            }
            catch
            {
            }
        }

        await StopReceiveLoopAsync();
        socket?.Dispose();
        if (ReferenceEquals(_webSocket, socket))
        {
            _webSocket = null;
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