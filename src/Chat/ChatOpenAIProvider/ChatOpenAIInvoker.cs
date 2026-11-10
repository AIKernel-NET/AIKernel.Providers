using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AIKernel.Abstractions.Capabilities;
using AIKernel.Abstractions.Providers;
using AIKernel.Dtos.Capabilities;

namespace AIKernel.Providers.ChatOpenAI;

/// <summary>
/// [EN] Capability module invoker for ChatOpenAI provider operations.
/// [JA] ChatOpenAI Provider operation 用の capability module invoker です。
/// </summary>
public sealed class ChatOpenAIInvoker : ICapabilityModuleInvoker
{
    private readonly ITextEmbeddingProvider? _embeddingProvider;

    /// <summary>
    /// [EN] Creates an invoker for manifest discovery without bound provider credentials.
    /// [JA] Providerが未接続の状態でマニフェスト検出用のInvokerを作成します。
    /// </summary>
    public ChatOpenAIInvoker()
    {
    }

    /// <summary>
    /// [EN] Binds the embedding operation to a host-configured provider.
    /// [JA] 埋め込み操作をホストで構成済みのProviderに接続します。
    /// </summary>
    public ChatOpenAIInvoker(ITextEmbeddingProvider embeddingProvider)
        => _embeddingProvider = embeddingProvider ?? throw new ArgumentNullException(nameof(embeddingProvider));

    /// <summary>
    /// [EN] Invokes the capability. Embedding takes the input argument and returns a JSON vector in metadata.
    /// [JA] Capabilityを実行します。埋め込みはinput引数を受け取り、メタデータでJSONのベクトルを返します。
    /// </summary>
    public async ValueTask<CapabilityInvocationResult> InvokeAsync(
        CapabilityInvocationRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(request);

        var metadata = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var item in request.Metadata)
        {
            metadata[item.Key] = item.Value;
        }

        metadata["provider"] = "ChatOpenAIProvider";
        metadata["operation"] = request.Operation;
        if (request.Operation != "embedding")
        {
            return request.Operation is "chat.completion" or "moderation"
                ? Result()
                : Result("OPENAI_OPERATION_NOT_SUPPORTED", $"Unsupported ChatOpenAI operation: {request.Operation}.");
        }

        metadata.Remove("embedding");
        metadata.Remove("dimensions");
        if (!request.Arguments.TryGetValue("input", out var input) || string.IsNullOrWhiteSpace(input))
        {
            return Result("OPENAI_EMBEDDING_INPUT_REQUIRED", "The embedding capability requires a non-empty input argument.");
        }

        if (_embeddingProvider is null)
        {
            return Result("OPENAI_EMBEDDING_NOT_CONFIGURED", "The embedding capability requires a configured provider.");
        }

        try
        {
            var vector = await _embeddingProvider.EmbedAsync(input, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            if (vector is not { Length: > 0 } || vector.Any(value => !float.IsFinite(value)))
            {
                return Result("OPENAI_EMBEDDING_FAILED", "The provider returned an invalid embedding vector.");
            }

            var json = ChatOpenAIClient.SerializeEmbedding(vector);
            metadata["embedding"] = json;
            metadata["dimensions"] = vector.Length.ToString(CultureInfo.InvariantCulture);
            return Result(hash: "sha256:" + Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(json))));
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or InvalidDataException
            or IOException or InvalidOperationException or OperationCanceledException)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Result("OPENAI_EMBEDDING_FAILED", "The configured provider could not generate the embedding.");
        }

        CapabilityInvocationResult Result(string? code = null, string? message = null, string? hash = null)
            => new(request.InvocationId, request.CapabilityId, code is null, hash, code, message,
                request.ReplayLogHash, metadata);
    }
}
