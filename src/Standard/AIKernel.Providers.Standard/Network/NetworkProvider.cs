namespace AIKernel.Providers.Standard.Network;

using AIKernel.Abstractions.Network;
using AIKernel.Common.Results;
using AIKernel.Providers.Standard;
using System.Net.Sockets;
using System.Net.WebSockets;

/// <summary>
/// [EN] Standard network provider for OS-level HTTP/WebSocket/WebRTC capability metadata.
/// [JA] OS-level HTTP/WebSocket/WebRTC capability metadata 向けの標準 network Provider です。
/// </summary>
public sealed class NetworkProvider : StandardProviderBase
{
    private readonly HttpClient _httpClient;

    /// <summary>[EN] Initializes the network provider. [JA] network Provider を初期化します。</summary>
    public NetworkProvider()
        : this(new HttpClient())
    {
    }

    /// <summary>[EN] Initializes the network provider with an HTTP client. [JA] HTTP client で network Provider を初期化します。</summary>
    public NetworkProvider(HttpClient httpClient)
        : base(
            "providers.network.standard",
            "Network Provider",
            global::AIKernel.Providers.ProviderBuildVersion.Value,
            ["network.http.get", "network.websocket.connect", "network.webrtc.signal"],
            ["http", "websocket", "webrtc"])
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
    }

    /// <summary>[EN] Executes an HTTP GET request. [JA] HTTP GET request を実行します。</summary>
    public Task<string> GetStringAsync(string uri, CancellationToken cancellationToken = default)
        => _httpClient.GetStringAsync(uri, cancellationToken);

    /// <summary>[EN] Safely executes an HTTP GET request as a Result. [JA] HTTP GET request を Result として安全に実行します。</summary>
    public Task<Result<string>> TryGetStringAsync(string uri, CancellationToken cancellationToken = default)
        => Try.RunAsync(async () =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            return await _httpClient.GetStringAsync(uri, cancellationToken).ConfigureAwait(false);
        });
}

/// <summary>
/// [EN] HTTP network provider implementing the Core network abstraction.
/// [JA] Core network 抽象を実装する HTTP network Provider です。
/// </summary>
public sealed class HttpNetworkProvider : StandardProviderBase, INetworkProvider
{
    private static readonly HttpClient SharedClient = new(new HttpClientHandler { AllowAutoRedirect = false });
    private readonly HttpClient _httpClient;
    private readonly HashSet<string> _allowedOrigins;

    /// <summary>[EN] Initializes the HTTP network provider. [JA] HTTP network Provider を初期化します。</summary>
    public HttpNetworkProvider()
        : this(SharedClient, [])
    {
    }

    /// <summary>[EN] Initializes the HTTP network provider with a client. [JA] client で HTTP network Provider を初期化します。</summary>
    public HttpNetworkProvider(HttpClient httpClient) : this(httpClient, []) { }

    internal HttpNetworkProvider(IEnumerable<string> allowedOrigins) : this(SharedClient, allowedOrigins) { }

    /// <summary>Allows exact HTTP, HTTPS and TCP origins. The supplied handler must disable automatic redirects.</summary>
    public HttpNetworkProvider(HttpClient httpClient, IEnumerable<string> allowedOrigins)
        : base("providers.network.http", "HTTP Network Provider", global::AIKernel.Providers.ProviderBuildVersion.Value, ["network.http.get", "network.http.post"], ["http"])
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        ArgumentNullException.ThrowIfNull(allowedOrigins);
        _allowedOrigins = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var origin in allowedOrigins)
        {
            if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https" or "tcp") ||
                string.IsNullOrEmpty(uri.Host) || uri.UserInfo.Length != 0 || uri.AbsolutePath != "/" ||
                uri.Query.Length != 0 || uri.Fragment.Length != 0 || uri.Port <= 0)
                throw new ArgumentException("Expected an HTTP, HTTPS or TCP origin with host and port.", nameof(allowedOrigins));
            _allowedOrigins.Add(uri.GetLeftPart(UriPartial.Authority));
        }
    }

    /// <summary>[EN] Documents this public package API member. [JA] ConnectAsync を実行します。</summary>
    /// <inheritdoc />
    public async Task<INetworkStream> ConnectAsync(string host, int port)
        => RequireSuccess(await TryConnectAsync(host, port).ConfigureAwait(false));

    /// <summary>[EN] Safely connects to a TCP endpoint as a Result. [JA] TCP endpoint へ Result として安全に接続します。</summary>
    public Task<Result<INetworkStream>> TryConnectAsync(string host, int port) => TryConnectAsync(host, port, CancellationToken.None);

    /// <summary>Runs the network operation with caller cancellation and the configured timeout.</summary>
    public async Task<INetworkStream> ConnectAsync(string host, int port, CancellationToken cancellationToken)
    {
        var result = await TryConnectAsync(host, port, cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        return RequireSuccess(result);
    }

    /// <summary>Runs the network operation with caller cancellation and the configured timeout.</summary>
    public Task<Result<INetworkStream>> TryConnectAsync(string host, int port, CancellationToken cancellationToken)
    {
        return
            from validHost in ValidateHost(host).AsTask()
            from validPort in ValidatePort(port).AsTask()
            from stream in Try.RunAsync<INetworkStream>(async () =>
            {
                if (Uri.CheckHostName(validHost) == UriHostNameType.Unknown ||
                    !_allowedOrigins.Contains(new UriBuilder("tcp", validHost, validPort).Uri.GetLeftPart(UriPartial.Authority)))
                    throw new InvalidOperationException("TCP destination is not permitted.");
                var client = new TcpClient();
                try
                {
                    using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                    timeout.CancelAfter(TimeSpan.FromSeconds(30));
                    await client.ConnectAsync(validHost, validPort, timeout.Token).ConfigureAwait(false);
                    return new TcpNetworkStream(client);
                }
                catch
                {
                    client.Dispose();
                    throw;
                }
            })
            select stream;
    }

    /// <summary>[EN] Documents this public package API member. [JA] HttpGetAsync を実行します。</summary>
    /// <inheritdoc />
    public Task<IHttpResponse> HttpGetAsync(string url) => HttpGetAsync(url, CancellationToken.None);

    /// <summary>Runs the network operation with caller cancellation and the configured timeout.</summary>
    public Task<IHttpResponse> HttpGetAsync(string url, CancellationToken token) => SendHttpAsync(HttpMethod.Get, url, null, token);

    /// <summary>[EN] Safely executes HTTP GET as a Result. [JA] HTTP GET を Result として安全に実行します。</summary>
    public Task<Result<IHttpResponse>> TryHttpGetAsync(string url)
    {
        return Try.RunAsync(() => HttpGetAsync(url));
    }

    /// <summary>[EN] Documents this public package API member. [JA] HttpPostAsync を実行します。</summary>
    /// <inheritdoc />
    public Task<IHttpResponse> HttpPostAsync(string url, byte[] body) => HttpPostAsync(url, body, CancellationToken.None);

    /// <summary>Runs the network operation with caller cancellation and the configured timeout.</summary>
    public Task<IHttpResponse> HttpPostAsync(string url, byte[] body, CancellationToken token) => SendHttpAsync(HttpMethod.Post, url, body, token);

    /// <summary>[EN] Safely executes HTTP POST as a Result. [JA] HTTP POST を Result として安全に実行します。</summary>
    public Task<Result<IHttpResponse>> TryHttpPostAsync(string url, byte[]? body)
    {
        return Try.RunAsync(() => HttpPostAsync(url, body ?? []));
    }

    private static Result<string> ValidateHost(string host)
        => string.IsNullOrWhiteSpace(host)
            ? Result<string>.Fail("Network host is required. ErrorCode=NETWORK_HOST_REQUIRED")
            : Result<string>.Success(host);

    private static Result<int> ValidatePort(int port)
        => port is > 0 and <= 65535
            ? Result<int>.Success(port)
            : Result<int>.Fail("Network port must be between 1 and 65535. ErrorCode=NETWORK_PORT_INVALID");

    private async Task<IHttpResponse> SendHttpAsync(HttpMethod method, string url, byte[]? body, CancellationToken token)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme is not ("https" or "http") ||
            uri.UserInfo.Length != 0 || !_allowedOrigins.Contains(uri.GetLeftPart(UriPartial.Authority)))
            throw new InvalidOperationException("HTTP destination is not permitted.");
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
        timeout.CancelAfter(_httpClient.Timeout);
        using var request = new HttpRequestMessage(method, uri) { Content = body is null ? null : new ByteArrayContent(body) };
        using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token).ConfigureAwait(false);
        await response.Content.LoadIntoBufferAsync(16 * 1024 * 1024, timeout.Token).ConfigureAwait(false);
        return await HttpNetworkResponse.FromAsync(response).ConfigureAwait(false);
    }


    private static T RequireSuccess<T>(Result<T> result)
        => result.Match(
            error => throw new InvalidOperationException(error.Message),
            value => value);
}

/// <summary>
/// [EN] WebSocket network provider implementing the Core network abstraction.
/// [JA] Core network 抽象を実装する WebSocket network Provider です。
/// </summary>
public sealed class WebSocketNetworkProvider()
    : StandardProviderBase("providers.network.websocket", "WebSocket Network Provider", global::AIKernel.Providers.ProviderBuildVersion.Value, ["network.websocket.connect"], ["websocket"]),
      INetworkProvider
{
    /// <summary>[EN] Documents this public package API member. [JA] ConnectAsync を実行します。</summary>
    /// <inheritdoc />
    public async Task<INetworkStream> ConnectAsync(string host, int port)
        => RequireSuccess(await TryConnectAsync(host, port).ConfigureAwait(false));

    /// <summary>[EN] Safely connects to a WebSocket endpoint as a Result. [JA] WebSocket endpoint へ Result として安全に接続します。</summary>
    public Task<Result<INetworkStream>> TryConnectAsync(string host, int port) => TryConnectAsync(host, port, CancellationToken.None);

    /// <summary>Runs the network operation with caller cancellation and the configured timeout.</summary>
    public async Task<INetworkStream> ConnectAsync(string host, int port, CancellationToken cancellationToken)
    {
        var result = await TryConnectAsync(host, port, cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        return RequireSuccess(result);
    }

    /// <summary>Runs the network operation with caller cancellation and the configured timeout.</summary>
    public Task<Result<INetworkStream>> TryConnectAsync(string host, int port, CancellationToken cancellationToken)
    {
        return
            from validHost in ValidateHost(host).AsTask()
            from validPort in ValidatePort(port).AsTask()
            from stream in Try.RunAsync<INetworkStream>(async () =>
            {
                var webSocket = new ClientWebSocket();
                try
                {
                    using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                    timeout.CancelAfter(TimeSpan.FromSeconds(30));
                    await webSocket.ConnectAsync(new Uri($"ws://{validHost}:{validPort}/"), timeout.Token).ConfigureAwait(false);
                    return new WebSocketNetworkStream(webSocket);
                }
                catch
                {
                    webSocket.Dispose();
                    throw;
                }
            })
            select stream;
    }

    /// <summary>[EN] Documents this public package API member. [JA] HttpGetAsync を実行します。</summary>
    /// <inheritdoc />
    public Task<IHttpResponse> HttpGetAsync(string url)
        => Task.FromException<IHttpResponse>(new NotSupportedException("WebSocketNetworkProvider does not perform HTTP GET."));

    /// <summary>[EN] Returns a fail-closed Result for unsupported HTTP GET. [JA] 未対応の HTTP GET を fail-closed Result として返します。</summary>
    public Task<Result<IHttpResponse>> TryHttpGetAsync(string url)
        => Result<IHttpResponse>.Fail("WebSocketNetworkProvider does not perform HTTP GET. ErrorCode=WEBSOCKET_HTTP_GET_UNSUPPORTED").AsTask();

    /// <summary>[EN] Documents this public package API member. [JA] HttpPostAsync を実行します。</summary>
    /// <inheritdoc />
    public Task<IHttpResponse> HttpPostAsync(string url, byte[] body)
        => Task.FromException<IHttpResponse>(new NotSupportedException("WebSocketNetworkProvider does not perform HTTP POST."));

    /// <summary>[EN] Returns a fail-closed Result for unsupported HTTP POST. [JA] 未対応の HTTP POST を fail-closed Result として返します。</summary>
    public Task<Result<IHttpResponse>> TryHttpPostAsync(string url, byte[]? body)
        => Result<IHttpResponse>.Fail("WebSocketNetworkProvider does not perform HTTP POST. ErrorCode=WEBSOCKET_HTTP_POST_UNSUPPORTED").AsTask();

    private static Result<string> ValidateHost(string host)
        => string.IsNullOrWhiteSpace(host)
            ? Result<string>.Fail("Network host is required. ErrorCode=NETWORK_HOST_REQUIRED")
            : Result<string>.Success(host);

    private static Result<int> ValidatePort(int port)
        => port is > 0 and <= 65535
            ? Result<int>.Success(port)
            : Result<int>.Fail("Network port must be between 1 and 65535. ErrorCode=NETWORK_PORT_INVALID");

    private static T RequireSuccess<T>(Result<T> result)
        => result.Match(
            error => throw new InvalidOperationException(error.Message),
            value => value);
}

/// <summary>[EN] Immutable HTTP response implementation. [JA] immutable HTTP response implementation です。</summary>
public sealed record HttpNetworkResponse(
    int StatusCode,
    byte[] Body,
    IReadOnlyDictionary<string, string> Headers) : IHttpResponse
{
    /// <summary>[EN] Creates a response from HttpResponseMessage. [JA] HttpResponseMessage から response を作成します。</summary>
    public static async Task<HttpNetworkResponse> FromAsync(HttpResponseMessage response)
        => RequireSuccess(await TryFromAsync(response).ConfigureAwait(false));

    /// <summary>[EN] Safely creates a response from HttpResponseMessage. [JA] HttpResponseMessage から response を安全に作成します。</summary>
    public static Task<Result<HttpNetworkResponse>> TryFromAsync(HttpResponseMessage? response)
    {
        return
            from validResponse in ValidateResponse(response).AsTask()
            from projected in Try.RunAsync(async () =>
            {
                var headers = validResponse.Headers
                    .Concat(validResponse.Content.Headers)
                    .ToDictionary(pair => pair.Key, pair => string.Join(",", pair.Value), StringComparer.Ordinal);
                return new HttpNetworkResponse(
                    (int)validResponse.StatusCode,
                    await validResponse.Content.ReadAsByteArrayAsync().ConfigureAwait(false),
                    headers);
            })
            select projected;
    }

    private static Result<HttpResponseMessage> ValidateResponse(HttpResponseMessage? response)
        => response is null
            ? Result<HttpResponseMessage>.Fail("HTTP response is required. ErrorCode=HTTP_RESPONSE_REQUIRED")
            : Result<HttpResponseMessage>.Success(response);

    private static T RequireSuccess<T>(Result<T> result)
        => result.Match(
            error => throw new InvalidOperationException(error.Message),
            value => value);
}

internal sealed class TcpNetworkStream(TcpClient client) : INetworkStream
{
    private readonly TcpClient _client = client ?? throw new ArgumentNullException(nameof(client));
    /// <summary>
    /// EN: Executes DisposeAsync.
    /// [EN] Documents this public package API member. [JA] DisposeAsync を実行します。
    /// </summary>

    public async ValueTask DisposeAsync()
    {
        _client.Dispose();
        await ValueTask.CompletedTask.ConfigureAwait(false);
    }
    /// <summary>
    /// EN: Executes ReadAsync.
    /// [EN] Documents this public package API member. [JA] ReadAsync を実行します。
    /// </summary>

    public ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        => _client.GetStream().ReadAsync(buffer, cancellationToken);
    /// <summary>
    /// EN: Executes WriteAsync.
    /// [EN] Documents this public package API member. [JA] WriteAsync を実行します。
    /// </summary>

    public ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        => _client.GetStream().WriteAsync(buffer, cancellationToken);
}

internal sealed class WebSocketNetworkStream(ClientWebSocket webSocket) : INetworkStream
{
    private readonly ClientWebSocket _webSocket = webSocket ?? throw new ArgumentNullException(nameof(webSocket));
    /// <summary>
    /// EN: Executes DisposeAsync.
    /// [EN] Documents this public package API member. [JA] DisposeAsync を実行します。
    /// </summary>

    public async ValueTask DisposeAsync()
    {
        await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "disposed", CancellationToken.None).ConfigureAwait(false);
        _webSocket.Dispose();
    }
    /// <summary>
    /// EN: Executes ReadAsync.
    /// [EN] Documents this public package API member. [JA] ReadAsync を実行します。
    /// </summary>

    public async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        var result = await _webSocket.ReceiveAsync(buffer, cancellationToken).ConfigureAwait(false);
        return result.Count;
    }
    /// <summary>
    /// EN: Executes WriteAsync.
    /// [EN] Documents this public package API member. [JA] WriteAsync を実行します。
    /// </summary>

    public async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        await _webSocket.SendAsync(buffer, WebSocketMessageType.Binary, true, cancellationToken).ConfigureAwait(false);
    }
}
