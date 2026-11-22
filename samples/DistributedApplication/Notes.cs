using System.Collections.Concurrent;
using System.Text.Json;
using AIKernel.Abstractions.Hosting;
using AIKernel.Dtos.Hosting;

namespace DistributedApplication.Sample;

/// <summary>A single in-memory application service shared by REST and MCP.</summary>
public sealed class Notes : IApiEndpointProvider, IMcpToolProvider
{
    private readonly ConcurrentDictionary<Guid, string> notes = new();
    private static readonly JsonElement schema = JsonSerializer.SerializeToElement(new
    {
        type = "object", properties = new { text = new { type = "string" } }, required = new[] { "text" }
    });

    public IEnumerable<ApiEndpoint> Endpoints => [
        new("POST", "/notes", "save", "client", "application/json"),
        new("GET", "/notes/{id:guid}", "read", "client")
    ];
    public IEnumerable<McpOperation> Tools => [new("save_note", "Save a note and return its ID.", schema, "save", "client")];

    public async ValueTask<ApiResponse> InvokeAsync(string operationId, ApiInvocation request, CancellationToken ct)
    {
        if (operationId == "read")
            return Guid.TryParse(request.RouteValues["id"], out var id) && notes.TryGetValue(id, out var text)
                ? new(200, JsonSerializer.SerializeToElement(new { text })) : new(404);
        using var document = await JsonDocument.ParseAsync(request.Body, cancellationToken: ct);
        return new(201, await CallAsync(operationId, document.RootElement, request.Caller, ct));
    }

    public ValueTask<JsonElement> CallAsync(string operationId, JsonElement input, RequestIdentity caller, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var id = Guid.NewGuid();
        notes[id] = input.GetProperty("text").GetString() ?? "";
        return ValueTask.FromResult(JsonSerializer.SerializeToElement(new { id }));
    }
}
