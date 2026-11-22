using System.Net.Http.Headers;
using AIKernel.Abstractions.Hosting;
using AIKernel.Dtos.Hosting;

namespace AIKernel.Providers.Distributed;

public sealed class ConnectionProfiles : IConnectionProfileStore, IConnectionClient, IDisposable
{
    private sealed record Registry(Guid? Selected, ConnectionDescriptor[] Servers);
    private readonly string path;
    private readonly IConnectionDescriptorProvider files;
    private readonly object gate = new();
    private readonly HttpClient http = new(new SocketsHttpHandler { AllowAutoRedirect = false, UseCookies = false })
        { Timeout = Timeout.InfiniteTimeSpan };
    public event Action? Changed;

    public ConnectionProfiles(string privateDirectory, IConnectionDescriptorProvider files)
    {
        this.files = files;
        path = Path.Combine(privateDirectory, "profiles.json");
        Directory.CreateDirectory(privateDirectory);
        Read();
    }

    private Registry Read()
    {
        if (!File.Exists(path)) return new(null, []);
        var registry = PrivateFiles.Read<Registry>(path);
        if (registry.Servers == null || registry.Servers.Any(x => x == null))
            throw new InvalidDataException("Invalid profile registry.");
        var ids = registry.Servers.Select(x => files.Validate(x).ServerId).ToHashSet();
        if (ids.Count != registry.Servers.Length || registry.Selected is Guid selected && !ids.Contains(selected))
            throw new InvalidDataException("Invalid profile selection.");
        return registry;
    }

    public Guid? Selected => Read().Selected;
    public IReadOnlyList<ConnectionProfile> List() => Read().Servers.Select(files.Validate).ToArray();

    private void Update(Func<Registry, Registry> update)
    {
        lock (gate)
        {
            using var lease = new FileStream(path + ".lock", FileMode.OpenOrCreate, FileAccess.Write, FileShare.None);
            PrivateFiles.Write(path, update(Read()));
        }
        Changed?.Invoke();
    }

    public void Import(Stream descriptor)
    {
        var entry = files.Read(descriptor);
        var id = files.Validate(entry).ServerId;
        Update(old => new(id, [..old.Servers.Where(x => files.Validate(x).ServerId != id), entry]));
    }

    public void Select(Guid serverId) => Update(old => old.Servers.Any(x => files.Validate(x).ServerId == serverId)
        ? old with { Selected = serverId } : throw new KeyNotFoundException("Unknown connection profile."));

    public async Task<HttpResponseMessage> SendAsync(Guid serverId, HttpMethod method, string relativePath,
        HttpContent? body = null, string? range = null, CancellationToken cancellationToken = default)
    {
        using var message = new HttpRequestMessage(method, new Uri(relativePath, UriKind.RelativeOrAbsolute)) { Content = body };
        if (range != null) message.Headers.Range = RangeHeaderValue.Parse(range);
        return await SendRequestAsync(serverId, message, cancellationToken);
    }

    public async Task<HttpResponseMessage> SendRequestAsync(Guid serverId, HttpRequestMessage message, CancellationToken cancellationToken = default)
    {
        if (message.RequestUri is not { IsAbsoluteUri: false } relative)
            throw new ArgumentException("An API-relative path is required.");
        var relativePath = relative.OriginalString;
        var entry = Read().Servers.FirstOrDefault(x => files.Validate(x).ServerId == serverId)
            ?? throw new KeyNotFoundException("Unknown connection profile.");
        var root = files.Validate(entry).ApiUrl;
        if (relativePath.StartsWith('/') || relativePath.Contains('\\') || !Uri.TryCreate(relativePath, UriKind.Relative, out var rel))
            throw new ArgumentException("An API-relative path is required.");
        var target = new Uri(root, rel);
        if (!root.IsBaseOf(target) || root.Authority != target.Authority || root.Scheme != target.Scheme ||
            target.UserInfo.Length != 0 || target.Fragment.Length != 0)
            throw new ArgumentException("Request is outside the selected API.");
        using var request = new HttpRequestMessage(message.Method, target) { Content = message.Content };
        foreach (var name in new[] { "Accept", "Range", "If-Range", "If-None-Match", "If-Modified-Since",
            "MCP-Protocol-Version", "Mcp-Session-Id", "Last-Event-ID" })
            if (message.Headers.TryGetValues(name, out var values)) request.Headers.TryAddWithoutValidation(name, values);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", entry.Token);
        try { return await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken); }
        finally { request.Headers.Authorization = null; request.Content = null; }
    }

    public void Dispose() => http.Dispose();
}
