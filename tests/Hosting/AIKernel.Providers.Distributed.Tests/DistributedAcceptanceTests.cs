using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using AIKernel.Abstractions.Hosting;
using AIKernel.Dtos.Hosting;
using AIKernel.Providers.Distributed;
using DistributedApplication.Sample;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace AIKernel.Providers.Distributed.Tests;

public sealed class DistributedAcceptanceTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "aikernel-distributed-" + Guid.NewGuid().ToString("N"));
    private readonly ConnectionFiles files = new();

    private DistributedHostOptions Options(string name, Uri? bind = null, Uri? advertised = null) => new()
    {
        Name = name, Service = "Example.Notes", BindAddress = bind ?? new("http://127.0.0.1:0"), AdvertisedAddress = advertised,
        PrivateDirectory = Path.Combine(root, name), ConnectionFile = Path.Combine(root, name, "connection.json")
    };

    private DistributedApplicationHost Host(DistributedHostOptions o, IApiEndpointProvider[]? api = null, IMcpToolProvider[]? tools = null)
    {
        var credentials = new ApplicationCredentials(o.PrivateDirectory);
        var notes = new Notes();
        var media = new Media();
        return new(o, credentials, credentials, files, api ?? [notes, media], tools ?? [notes, media]);
    }

    private ConnectionDescriptor Read(DistributedHostOptions o)
    {
        using var stream = File.OpenRead(o.ConnectionFile);
        return files.Read(stream);
    }
    private static void Import(ConnectionProfiles store, DistributedHostOptions o)
    {
        using var stream = File.OpenRead(o.ConnectionFile);
        store.Import(stream);
    }

    [Fact]
    public async Task ConnectionFileCanBeReplacedAfterAReaderCloses()
    {
        var options = Options("notes");
        await using var host = Host(options);
        await host.StartAsync();
        var descriptor = Read(options);
        var changed = descriptor with { Name = "Updated name" };
        using var started = new ManualResetEventSlim();
        Task writing;
        using (File.OpenRead(options.ConnectionFile))
        {
            writing = Task.Run(() =>
            {
                started.Set();
                files.Export(options.ConnectionFile, changed);
            });
            Assert.True(started.Wait(TimeSpan.FromSeconds(5)));
            await Task.Delay(75, TestContext.Current.CancellationToken);
        }
        await writing;
        Assert.Equal(changed, Read(options));
    }

    [Fact]
    public async Task SdkToolAndRestShareNotesAndEnforceScopes()
    {
        var o = Options("notes"); await using var host = Host(o);
        await host.StartAsync();
        var connection = Read(o);
        using var http = new HttpClient();
        foreach (var path in new[] { "/heartbeat", "/notes", "/mcp" })
            using (var unauthorized = await http.GetAsync(connection.ApiUrl + path))
                Assert.Equal(HttpStatusCode.Unauthorized, unauthorized.StatusCode);
        foreach (var method in new[] { "initialize", "tools/list", "tools/call" })
        {
            using var unauthorized = await http.PostAsJsonAsync(connection.McpUrl, new {jsonrpc="2.0",id=1,method}, TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.Unauthorized, unauthorized.StatusCode);
        }
        await using var mcp = await McpClient.CreateAsync(new HttpClientTransport(new HttpClientTransportOptions
        {
            Endpoint = new(connection.McpUrl), TransportMode = HttpTransportMode.StreamableHttp,
            AdditionalHeaders = new Dictionary<string,string> { ["Authorization"] = "Bearer " + connection.Token }
        }));
        var list = await mcp.ListToolsAsync();
        Assert.Equal("save_note", Assert.Single(list).Name);
        var saved = await mcp.CallToolAsync("save_note", new Dictionary<string,object?> { ["text"] = "sample" });
        var id = JsonSerializer.Deserialize<JsonElement>(((TextContentBlock)saved.Content.Single()).Text).GetProperty("id").GetGuid();
        http.DefaultRequestHeaders.Authorization = new("Bearer", connection.Token);
        Assert.Equal("sample", (await http.GetFromJsonAsync<JsonElement>(connection.ApiUrl + "/notes/" + id)).GetProperty("text").GetString());
        using var denied = await http.GetAsync(connection.ApiUrl + "/worker");
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        using var request = new HttpRequestMessage(HttpMethod.Post, connection.McpUrl)
        {
            Content = JsonContent.Create(new {jsonrpc="2.0",id=2,method="tools/call",@params=new {name="worker_tool", arguments=new {}}})
        };
        request.Headers.Accept.ParseAdd("application/json, text/event-stream");
        using var deniedTool = await http.SendAsync(request);
        Assert.Equal(HttpStatusCode.Forbidden, deniedTool.StatusCode);
        var credentials = new ApplicationCredentials(o.PrivateDirectory);
        http.DefaultRequestHeaders.Authorization = new("Bearer", await credentials.GetSecretAsync("worker"));
        using var allowed = await http.GetAsync(connection.ApiUrl + "/worker");
        Assert.Equal(HttpStatusCode.NoContent, allowed.StatusCode);
        credentials.Rotate();
        http.DefaultRequestHeaders.Authorization = new("Bearer", connection.Token);
        using var revoked = await http.GetAsync(connection.ApiUrl + "/heartbeat");
        Assert.Equal(HttpStatusCode.Unauthorized, revoked.StatusCode);
        using var revokedTool = await http.PostAsJsonAsync(connection.McpUrl,
            new {jsonrpc="2.0",id=2,method="tools/list"}, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Unauthorized, revokedTool.StatusCode);
    }

    [Fact]
    public async Task PinnedProfilesPersistAndTransferRangeWithoutFollowingRedirects()
    {
        var a = Options("home"); var b = Options("office");
        await using var first = Host(a); await using var second = Host(b);
        await first.StartAsync(); await second.StartAsync();
        var storePath = Path.Combine(root, "profiles");
        using var store = new ConnectionProfiles(storePath, files);
        Assert.Empty(store.List());
        Import(store, a); var idA = store.Selected!.Value; Import(store, b); var idB = store.Selected!.Value;
        var media = store.SendAsync(idA, HttpMethod.Get, "audio", range:"bytes=2-4");
        store.Select(idB);
        using var result = await media;
        Assert.Equal(HttpStatusCode.PartialContent, result.StatusCode);
        Assert.Equal("bytes 2-4/8", result.Content.Headers.ContentRange!.ToString());
        Assert.Equal("cde", await result.Content.ReadAsStringAsync());
        using var invalid = new MemoryStream("{}"u8.ToArray());
        Assert.Throws<InvalidDataException>(() => store.Import(invalid));
        using var broken = new MemoryStream("{"u8.ToArray());
        Assert.Throws<InvalidDataException>(() => store.Import(broken));
        using var restored = new ConnectionProfiles(storePath, files);
        Assert.Equal(2, restored.List().Count); Assert.Equal(idB, restored.Selected);
        await Assert.ThrowsAsync<ArgumentException>(() => store.SendAsync(idA, HttpMethod.Get, Read(b).ApiUrl));
        using var wrong = new HttpClient();
        wrong.DefaultRequestHeaders.Authorization = new("Bearer", Read(a).Token);
        using var denied = await wrong.GetAsync(Read(b).ApiUrl + "/heartbeat");
        Assert.Equal(HttpStatusCode.Unauthorized, denied.StatusCode);
        Assert.DoesNotContain(Read(a).Token, JsonSerializer.Serialize(store.List()));
        if (!OperatingSystem.IsWindows())
            Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(a.ConnectionFile));
    }

    [Fact]
    public async Task MonitorSeparatesLossRecoveryRevocationAndIdentityMismatch()
    {
        var a = Options("a"); var b = Options("b");
        await using var first = Host(a); await using var second = Host(b);
        await first.StartAsync(); await second.StartAsync();
        using var store = new ConnectionProfiles(Path.Combine(root,"profiles"), files);
        var monitor = new HeartbeatMonitor(store, store, TimeSpan.FromMilliseconds(50), TimeSpan.FromSeconds(1));
        using var cancel = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var monitoring = monitor.MonitorAsync(cancel.Token);
        try
        {
            Import(store,a); var id = store.Selected!.Value; Import(store,b); var other = store.Selected!.Value;
            async Task Wait(Guid key, ConnectionState state)
            {
                while (monitor.Get(key).State != state) await Task.Delay(20, cancel.Token);
            }
            await Wait(id,ConnectionState.Online); await Wait(other,ConnectionState.Online);
            await first.StopAsync(); await Wait(id,ConnectionState.Unavailable);
            Assert.Equal(ConnectionState.Online,monitor.Get(other).State); Assert.NotNull(monitor.Get(id).LastSuccess);
            await first.StartAsync(); Import(store,a); Assert.Equal(id,store.Selected); await Wait(id,ConnectionState.Online);
            new ApplicationCredentials(a.PrivateDirectory).Rotate(); await Wait(id,ConnectionState.Unauthorized);
            await first.ExportConnectionAsync(cancel.Token); Import(store,a); await Wait(id,ConnectionState.Online);
            var alias = Read(a) with { ServerId=Guid.NewGuid().ToString("N") };
            var tmp = Path.Combine(root,"mismatch.json"); files.Export(tmp,alias);
            using(var input = File.OpenRead(tmp)) store.Import(input);
            await Wait(store.Selected!.Value,ConnectionState.IdentityMismatch);
        }
        finally { cancel.Cancel(); await Assert.ThrowsAnyAsync<OperationCanceledException>(() => monitoring); }
    }

    [Fact]
    public async Task InvalidRegistrationAndOccupiedPortDoNotExport()
    {
        var o = Options("duplicates"); var notes = new Notes();
        await using var duplicate = Host(o, [notes, notes]);
        await Assert.ThrowsAsync<InvalidOperationException>(() => duplicate.StartAsync()); Assert.False(File.Exists(o.ConnectionFile));
        await using var tools = Host(o, tools:[notes,notes]);
        await Assert.ThrowsAsync<ArgumentException>(() => tools.StartAsync()); Assert.False(File.Exists(o.ConnectionFile));
        await using var first = Host(o); await first.StartAsync();
        var conflict = Options("conflict", new Uri(Read(o).ApiUrl)); await using var collision = Host(conflict);
        await Assert.ThrowsAsync<IOException>(() => collision.StartAsync()); Assert.False(File.Exists(conflict.ConnectionFile));
        var proxy = Options("proxy", advertised:new("https://notes.example:8443/service/team"));
        await using var behindProxy = Host(proxy); await behindProxy.StartAsync();
        Assert.Equal("https://notes.example:8443/service/team/mcp",Read(proxy).McpUrl);
    }

    public void Dispose() { if (Directory.Exists(root)) Directory.Delete(root,true); }

    private sealed class Media : IApiEndpointProvider, IMcpToolProvider
    {
        public IEnumerable<ApiEndpoint> Endpoints => [new("GET","audio","audio","client"),new("GET","worker","work","worker")];
        public IEnumerable<McpOperation> Tools => [new("worker_tool","worker only",JsonSerializer.SerializeToElement(new {type="object"}),"work","worker")];
        public ValueTask<JsonElement> CallAsync(string id,JsonElement input,RequestIdentity caller,CancellationToken ct) =>
            ValueTask.FromResult(JsonSerializer.SerializeToElement(true));
        public ValueTask<ApiResponse> InvokeAsync(string id,ApiInvocation request,CancellationToken ct) =>
            ValueTask.FromResult(id=="work" ? new ApiResponse(204) :
                new ApiResponse(200,Body:new MemoryStream("abcdefgh"u8.ToArray()),ContentType:"audio/wav",EnableRanges:true));
    }
}
