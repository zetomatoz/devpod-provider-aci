using System.Net.WebSockets;

namespace DevPod.Provider.ACI.Infrastructure;

public interface IWebSocketClient : IAsyncDisposable
{
    Task ConnectAsync(Uri uri, CancellationToken cancellationToken);

    Task SendAsync(ArraySegment<byte> buffer, WebSocketMessageType messageType, bool endOfMessage, CancellationToken cancellationToken);

    Task<WebSocketReceiveResult> ReceiveAsync(ArraySegment<byte> buffer, CancellationToken cancellationToken);

    Task CloseAsync(WebSocketCloseStatus closeStatus, string? statusDescription, CancellationToken cancellationToken);

    WebSocketState State { get; }
}

public interface IWebSocketClientFactory
{
    IWebSocketClient Create();
}

internal sealed class ClientWebSocketAdapter : IWebSocketClient
{
    private readonly ClientWebSocket _client = new();

    public ClientWebSocketAdapter()
    {
        _client.Options.KeepAliveInterval = TimeSpan.FromSeconds(10);
    }

    public Task ConnectAsync(Uri uri, CancellationToken cancellationToken) =>
        _client.ConnectAsync(uri, cancellationToken);

    public Task SendAsync(ArraySegment<byte> buffer, WebSocketMessageType messageType, bool endOfMessage, CancellationToken cancellationToken) =>
        _client.SendAsync(buffer, messageType, endOfMessage, cancellationToken);

    public Task<WebSocketReceiveResult> ReceiveAsync(ArraySegment<byte> buffer, CancellationToken cancellationToken) =>
        _client.ReceiveAsync(buffer, cancellationToken);

    public Task CloseAsync(WebSocketCloseStatus closeStatus, string? statusDescription, CancellationToken cancellationToken) =>
        _client.CloseAsync(closeStatus, statusDescription, cancellationToken);

    public WebSocketState State => _client.State;

    public async ValueTask DisposeAsync()
    {
        if (_client.State is WebSocketState.Open or WebSocketState.CloseReceived)
        {
            await _client.CloseAsync(WebSocketCloseStatus.NormalClosure, "Disposing", CancellationToken.None).ConfigureAwait(false);
        }

        _client.Dispose();
    }
}

internal sealed class DefaultWebSocketClientFactory : IWebSocketClientFactory
{
    public IWebSocketClient Create() => new ClientWebSocketAdapter();
}
