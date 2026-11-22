using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using AIKernel.Abstractions.Hosting;
using AIKernel.Dtos.Hosting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.AspNetCore;
using ModelContextProtocol.Protocol;

namespace AIKernel.Providers.Distributed;

public sealed class DistributedApplicationHost(DistributedHostOptions options, IApplicationIdentity credentials,
    IRequestTokenValidator validator, IConnectionDescriptorProvider descriptors,
    IEnumerable<IApiEndpointProvider> endpoints, IEnumerable<IMcpToolProvider> tools,
    ILogger<DistributedApplicationHost>? logger = null) : IDistributedApplicationHost
{
    private WebApplication? app;
    private readonly SemaphoreSlim lifecycle = new(1);
    private static readonly object IdentityKey = new();

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        await lifecycle.WaitAsync(cancellationToken);
        try
        {
            if (app != null) return;
            var bind = options.BindAddress;
            if (bind.AbsolutePath != "/" || bind.Query.Length != 0 || bind.Fragment.Length != 0 || bind.UserInfo.Length != 0 ||
                !(bind.Scheme == "https" || bind.Scheme == "http" && (bind.IsLoopback || options.AllowPlainHttp || options.TrustedProxies.Count > 0)))
                throw new ArgumentException("Invalid bind address or transport policy.");
            if (options.StaticDirectory != null) PrivateFiles.CheckPath(options.StaticDirectory);
            foreach (var path in new[] { options.PrivateDirectory, options.ConnectionFile })
            {
                PrivateFiles.CheckPath(path);
                if (options.StaticDirectory != null && IsInside(options.StaticDirectory, path))
                    throw new ArgumentException("Private files require a location outside the static directory.");
            }
            var builder = WebApplication.CreateSlimBuilder();
            builder.Logging.ClearProviders();
            builder.WebHost.UseUrls(bind.AbsoluteUri);
            builder.Services.AddHttpContextAccessor();
            var operations = tools.SelectMany(p => p.Tools.Select(t => (Tool:t, Provider:p)))
                .ToDictionary(x => x.Tool.Name, StringComparer.Ordinal);
            builder.Services.AddMcpServer().WithHttpTransport(o => o.SessionMode = HttpServerSessionMode.Stateless)
                .WithListToolsHandler((request, ct) =>
                {
                    var identity = Caller(request.Services!.GetRequiredService<IHttpContextAccessor>().HttpContext!);
                    return ValueTask.FromResult(new ListToolsResult { Tools = operations.Values.Where(x => identity.Scopes.Contains(x.Tool.Scope))
                        .Select(x => new Tool { Name=x.Tool.Name, Description=x.Tool.Description, InputSchema=x.Tool.InputSchema }).ToList() });
                }).WithCallToolHandler(async (request, ct) =>
                {
                    var identity = Caller(request.Services!.GetRequiredService<IHttpContextAccessor>().HttpContext!);
                    if (request.Params == null || !operations.TryGetValue(request.Params.Name, out var item) || !identity.Scopes.Contains(item.Tool.Scope))
                        throw new InvalidOperationException("Tool is unavailable.");
                    var input = JsonSerializer.SerializeToElement(request.Params.Arguments ?? new Dictionary<string, JsonElement>());
                    var result = await item.Provider.CallAsync(item.Tool.OperationId, input, identity, ct);
                    return new CallToolResult { Content = [new TextContentBlock { Text = result.GetRawText() }] };
                });
            var web = builder.Build();
            try
            {
                if (options.TrustedProxies.Count > 0)
                {
                    var forwarded = new ForwardedHeadersOptions { ForwardedHeaders = ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedFor };
                    forwarded.KnownIPNetworks.Clear();
                    forwarded.KnownProxies.Clear();
                    foreach (var address in options.TrustedProxies) forwarded.KnownProxies.Add(address);
                    web.UseForwardedHeaders(forwarded);
                }
                var serverId = credentials.ServerId;
                if (options.AdvertisedAddress is { AbsolutePath: not "/" } external)
                    web.UsePathBase(external.AbsolutePath.TrimEnd('/'));
                web.Use(async (ctx, next) =>
                {
                    ctx.Response.Headers.CacheControl = "no-store";
                    if (!ctx.Request.IsHttps && !options.AllowPlainHttp && !bind.IsLoopback)
                    {
                        ctx.Response.StatusCode = 403;
                        return;
                    }
                    var auth = ctx.Request.Headers.Authorization;
                    if (auth.Count != 1 || !AuthenticationHeaderValue.TryParse(auth[0], out var header) ||
                        !header.Scheme.Equals("Bearer", StringComparison.OrdinalIgnoreCase) || header.Parameter == null ||
                        await validator.ValidateAsync(header.Parameter, ctx.RequestAborted) is not { } caller)
                    {
                        ctx.Response.Headers.WWWAuthenticate = "Bearer";
                        ctx.Response.StatusCode = 401;
                        return;
                    }
                    ctx.Items[IdentityKey] = caller;
                    if (ctx.Request.Path.StartsWithSegments("/mcp") &&
                        HttpMethods.IsPost(ctx.Request.Method) && operations.Values.Any(x => !caller.Scopes.Contains(x.Tool.Scope)) &&
                        await IsForbiddenToolCall(ctx, name => operations.TryGetValue(name, out var op) && !caller.Scopes.Contains(op.Tool.Scope)))
                    {
                        ctx.Response.StatusCode = 403;
                        return;
                    }
                    var origin = ctx.Request.Headers.Origin;
                    if (origin.Count != 0 && origin.ToString() != advertised?.GetLeftPart(UriPartial.Authority))
                    {
                        ctx.Response.StatusCode = 403;
                        return;
                    }
                    await next(ctx);
                });
                RegisterRoutes(web, serverId);
                web.MapMcp("/mcp");
                await web.StartAsync(cancellationToken);
                var actual = web.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
                advertised = new ConnectionFiles(options.AllowPlainHttp).Base(options.AdvertisedAddress?.AbsoluteUri ?? actual);
                await WriteDescriptor(advertised, serverId, cancellationToken);
                app = web;
                ListeningAddress = new Uri(actual);
                logger?.LogInformation("Connection file written to {Path}", Path.GetFullPath(options.ConnectionFile));
            }
            catch { await web.DisposeAsync(); throw; }
        }
        finally { lifecycle.Release(); }
    }

    private Uri? advertised;
    private async Task WriteDescriptor(Uri address, string serverId, CancellationToken ct)
    {
        descriptors.Export(options.ConnectionFile, new(1, options.Service, serverId, options.Name,
            address.AbsoluteUri.TrimEnd('/'), new Uri(address, "mcp").AbsoluteUri, await credentials.GetClientTokenAsync(ct)));
    }

    public async Task ExportConnectionAsync(CancellationToken cancellationToken = default)
    {
        await lifecycle.WaitAsync(cancellationToken);
        try
        {
            if (app == null || advertised == null) throw new InvalidOperationException("Start the listener before exporting a connection.");
            await WriteDescriptor(advertised, credentials.ServerId, cancellationToken);
        }
        finally { lifecycle.Release(); }
    }
    /// <summary>The active local listener, including the port assigned by the operating system.</summary>
    public Uri? ListeningAddress { get; private set; }
    // Scope failures are HTTP 403. The SDK owns validation and dispatch of all JSON-RPC messages.
    private static async Task<bool> IsForbiddenToolCall(HttpContext context, Func<string, bool> forbidden)
    {
        const int maxBytes = 1024 * 1024;
        var limits = context.Features.Get<Microsoft.AspNetCore.Http.Features.IHttpMaxRequestBodySizeFeature>();
        if (limits is { IsReadOnly: false }) limits.MaxRequestBodySize = maxBytes;
        context.Request.EnableBuffering(64 * 1024, maxBytes);
        try
        {
            using var document = await JsonDocument.ParseAsync(context.Request.Body, cancellationToken: context.RequestAborted);
            var message = document.RootElement;
            return message.ValueKind == JsonValueKind.Object &&
                message.TryGetProperty("method", out var method) && method.ValueKind == JsonValueKind.String && method.ValueEquals("tools/call") &&
                message.TryGetProperty("params", out var args) && args.ValueKind == JsonValueKind.Object &&
                args.TryGetProperty("name", out var name) && name.ValueKind == JsonValueKind.String && forbidden(name.GetString()!);
        }
        catch (JsonException) { return false; }
        finally { context.Request.Body.Position = 0; }
    }
    private static RequestIdentity Caller(HttpContext ctx) => (RequestIdentity)ctx.Items[IdentityKey]!;
    private static bool IsInside(string directory, string path)
    {
        var relative = Path.GetRelativePath(directory, path);
        return !Path.IsPathRooted(relative) && relative != ".." && !relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal);
    }

    private void RegisterRoutes(WebApplication web, string serverId)
    {
        var routes = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "GET /heartbeat" };
        web.MapGet("/heartbeat", () => Results.Json(new Heartbeat(options.Service, serverId, "online", DateTimeOffset.UtcNow), PrivateFiles.Json));
        foreach (var provider in endpoints)
        foreach (var endpoint in provider.Endpoints)
        {
            var path = "/" + endpoint.Path.Trim('/');
            var parsed = RoutePatternFactory.Parse(path);
            var normalized = string.Join("/", parsed.PathSegments.Select(s => string.Concat(s.Parts.Select(RouteKeyPart))));
            if (path.Equals("/mcp", StringComparison.OrdinalIgnoreCase) || path.StartsWith("/mcp/", StringComparison.OrdinalIgnoreCase) ||
                !routes.Add(endpoint.Method + " /" + normalized))
                throw new InvalidOperationException("Duplicate or reserved HTTP route: " + path);
            web.MapMethods(path, [endpoint.Method], async (HttpContext ctx) =>
            {
                var caller = Caller(ctx);
                if (!caller.Scopes.Contains(endpoint.Scope)) { ctx.Response.StatusCode = 403; return; }
                if (endpoint.RequestContentType != null && !string.Equals(ctx.Request.ContentType?.Split(';')[0].Trim(),
                    endpoint.RequestContentType, StringComparison.OrdinalIgnoreCase)) { ctx.Response.StatusCode = 415; return; }
                var input = new ApiInvocation(ctx.Request.Body, ctx.Request.RouteValues.ToDictionary(x => x.Key, x => x.Value?.ToString() ?? ""),
                    ctx.Request.Query.ToDictionary(x => x.Key, x => x.Value.ToString()), ctx.Request.Headers.Range, caller);
                var result = await provider.InvokeAsync(endpoint.OperationId, input, ctx.RequestAborted);
                ctx.Response.StatusCode = result.StatusCode;
                if (result.Location != null) ctx.Response.Headers.Location = result.Location;
                if (result.Body != null)
                    await Results.Stream(result.Body, result.ContentType ?? endpoint.ResponseContentType, enableRangeProcessing:result.EnableRanges).ExecuteAsync(ctx);
                else if (result.Json is { } json)
                    await Results.Json(json, contentType:result.ContentType ?? endpoint.ResponseContentType, statusCode:result.StatusCode).ExecuteAsync(ctx);
            });
        }
    }

    private static string RouteKeyPart(RoutePatternPart part) => part switch
    {
        RoutePatternLiteralPart text => text.Content,
        RoutePatternSeparatorPart separator => separator.Content,
        RoutePatternParameterPart parameter => "{" + (parameter.IsCatchAll ? "*" : "") +
            string.Concat(parameter.ParameterPolicies.Select(policy => ":" + policy.Content)) +
            (parameter.IsOptional ? "?" : "") + "}",
        _ => throw new InvalidOperationException("Unsupported endpoint path.")
    };

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        await lifecycle.WaitAsync(cancellationToken);
        try
        {
            if (app != null)
            {
                try { await app.StopAsync(cancellationToken); }
                finally { await app.DisposeAsync(); app = null; ListeningAddress = null; }
            }
        }
        finally { lifecycle.Release(); }
    }

    public async ValueTask DisposeAsync() => await StopAsync();
}
