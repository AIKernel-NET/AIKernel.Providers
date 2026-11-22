using System.Net;
namespace AIKernel.Providers.Distributed;

public sealed class DistributedHostOptions
{
    public required string Service { get; init; }
    public required string Name { get; init; }
    public required string PrivateDirectory { get; init; }
    public required string ConnectionFile { get; init; }
    public string? StaticDirectory { get; init; }
    public Uri BindAddress { get; init; } = new("http://127.0.0.1:0");
    public Uri? AdvertisedAddress { get; init; }
    public bool AllowPlainHttp { get; init; }
    public IReadOnlyList<IPAddress> TrustedProxies { get; init; } = [];
}
