using AIKernel.Abstractions.Hosting;
using AIKernel.Dtos.Hosting;
using System.Text.Json;

namespace AIKernel.Providers.Distributed;

public sealed class ConnectionFiles(bool allowPlainHttp = false) : IConnectionDescriptorProvider
{
    internal Uri Base(string value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) ||
            !(uri.Scheme == "https" || uri.Scheme == "http" && (allowPlainHttp || uri.IsLoopback)) ||
            uri.UserInfo.Length != 0 || uri.Query.Length != 0 || uri.Fragment.Length != 0 ||
            uri.Host is "0.0.0.0" or "[::]" || uri.AbsolutePath.Contains("%", StringComparison.Ordinal))
            throw new InvalidDataException("Invalid advertised address or transport policy.");
        return new Uri(uri.AbsoluteUri.TrimEnd('/') + "/");
    }

    public ConnectionProfile Validate(ConnectionDescriptor descriptor)
    {
        if (descriptor.SchemaVersion != 1 || string.IsNullOrWhiteSpace(descriptor.Service) ||
            string.IsNullOrWhiteSpace(descriptor.Name) || !Guid.TryParseExact(descriptor.ServerId, "N", out var id) ||
            descriptor.Token is not { Length: >= 32 and <= 1024 } || descriptor.Token.Any(c => !char.IsAsciiLetterOrDigit(c) && c is not '-' and not '_'))
            throw new InvalidDataException("Invalid connection descriptor.");
        var api = Base(descriptor.ApiUrl);
        var mcp = new Uri(api, "mcp");
        if (!string.Equals(mcp.AbsoluteUri, descriptor.McpUrl, StringComparison.Ordinal))
            throw new InvalidDataException("The MCP address must belong to the advertised server.");
        return new(id, descriptor.Name, descriptor.Service, api, mcp);
    }

    public ConnectionDescriptor Read(Stream input)
    {
        using var buffer = new MemoryStream();
        Span<byte> chunk = stackalloc byte[1024];
        int n;
        while ((n = input.Read(chunk)) > 0)
        {
            if (buffer.Length + n > 16384) throw new InvalidDataException("Connection file exceeds 16 KiB.");
            buffer.Write(chunk[..n]);
        }
        ConnectionDescriptor descriptor;
        try { descriptor = JsonSerializer.Deserialize<ConnectionDescriptor>(buffer.ToArray(), PrivateFiles.Json)
            ?? throw new InvalidDataException("Empty connection descriptor."); }
        catch (JsonException) { throw new InvalidDataException("Invalid connection descriptor JSON."); }
        Validate(descriptor);
        return descriptor;
    }

    public void Export(string path, ConnectionDescriptor descriptor)
    {
        Validate(descriptor);
        PrivateFiles.Write(path, descriptor);
    }
}
