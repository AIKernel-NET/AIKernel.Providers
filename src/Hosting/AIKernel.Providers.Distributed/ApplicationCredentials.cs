using AIKernel.Abstractions.Hosting;
using AIKernel.Abstractions.Security;
using AIKernel.Dtos.Hosting;
using System.Security.Cryptography;
using System.Text;

namespace AIKernel.Providers.Distributed;

/// <summary>Persistent identity and rotating, independently scoped bearer credentials.</summary>
public sealed class ApplicationCredentials : ISecureCredentialProvider, IRequestTokenValidator, IApplicationIdentity
{
    private readonly string path;
    private readonly ISecureCredentialProvider secrets;
    private sealed record Keys(string ServerId, string ClientToken, string WorkerToken)
    {
        public override string ToString() => ServerId;
    }

    public ApplicationCredentials(string directory, ISecureCredentialProvider? secretSource = null)
    {
        path = Path.Combine(directory, "server-access.json");
        secrets = secretSource ?? this;
    }

    private Keys Load()
    {
        if (!File.Exists(path))
        {
            try { PrivateFiles.Write(path, new Keys(Guid.NewGuid().ToString("N"), NewToken(), NewToken()), false); }
            catch (IOException) when (File.Exists(path)) { }
        }
        var keys = PrivateFiles.Read<Keys>(path);
        if (!Guid.TryParseExact(keys.ServerId, "N", out _) || !ValidToken(keys.ClientToken) ||
            !ValidToken(keys.WorkerToken) || keys.ClientToken == keys.WorkerToken)
            throw new InvalidDataException("Invalid server identity or credentials.");
        return keys;
    }

    public string ServerId => Load().ServerId;
    public ValueTask<string> GetClientTokenAsync(CancellationToken ct) => secrets.GetSecretAsync("client", ct);
    private static string NewToken() => Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(32));
    private static bool ValidToken(string? value) => value is { Length: >= 32 and <= 1024 } &&
        value.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_');

    public void Rotate()
    {
        if (!ReferenceEquals(secrets, this)) throw new InvalidOperationException("Rotate credentials at the configured source.");
        PrivateFiles.Write(path, new Keys(ServerId, NewToken(), NewToken()));
    }

    public ValueTask<string> GetSecretAsync(string key, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var keys = Load();
        return ValueTask.FromResult(key switch { "client" => keys.ClientToken, "worker" => keys.WorkerToken,
            _ => throw new KeyNotFoundException("Unknown credential identifier.") });
    }

    public async ValueTask<RequestIdentity?> ValidateAsync(string bearer, CancellationToken cancellationToken)
    {
        if (bearer.Length is < 32 or > 1024) return null;
        string client = await secrets.GetSecretAsync("client", cancellationToken);
        string worker = await secrets.GetSecretAsync("worker", cancellationToken);
        if (client == worker) throw new InvalidOperationException("Client and worker credentials must be distinct.");
        byte[] presented = SHA256.HashData(Encoding.UTF8.GetBytes(bearer));
        foreach (var (id, token) in new[] { ("client", client), ("worker", worker) })
            if (CryptographicOperations.FixedTimeEquals(presented, SHA256.HashData(Encoding.UTF8.GetBytes(token))))
                return new(id, new HashSet<string>(StringComparer.Ordinal) { id });
        return null;
    }
}
