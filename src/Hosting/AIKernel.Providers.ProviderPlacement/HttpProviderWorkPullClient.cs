using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using AIKernel.Abstractions.Hosting;
using AIKernel.Abstractions.Providers;
using AIKernel.Dtos.Execution;
using AIKernel.Dtos.Providers;

namespace AIKernel.Providers.ProviderPlacement;

/// <summary>[EN] Pulls from one configured AIKernel connection profile. [JA] 構成済みの AIKernel 接続プロファイルからジョブを取得します。</summary>
public sealed class HttpProviderWorkPullClient(IConnectionClient connection, Guid centralServerId) : IProviderWorkPullClient
{
    private const string Prefix = "/api/v1/provider-work/";

    /// <inheritdoc />
    public async ValueTask<ProviderWorkItem?> ClaimAsync(string nodeId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentException.ThrowIfNullOrWhiteSpace(nodeId);
        using var content = JsonContent.Create(new ClaimRequest(nodeId), PullJson.Default.ClaimRequest);
        using var response = await SendAsync("claim", content, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NoContent) return null;
        if (response.StatusCode != HttpStatusCode.OK) throw ExecutionFailed();
        ProviderWorkItem? work;
        try
        {
            work = await response.Content.ReadFromJsonAsync(PullJson.Default.ProviderWorkItem, cancellationToken);
        }
        catch (JsonException)
        {
            throw ProviderPayloadVerifier.InvalidOutput();
        }
        if (work is null || string.IsNullOrWhiteSpace(work.JobId) || string.IsNullOrWhiteSpace(work.ClaimId) ||
            work.Requirements is null || string.IsNullOrWhiteSpace(work.Requirements.ProviderId) ||
            string.IsNullOrWhiteSpace(work.Requirements.ModelId) || !ProviderPayloadVerifier.IsValid(work.Input))
            throw ProviderPayloadVerifier.InvalidOutput();
        return work;
    }

    /// <inheritdoc />
    public async ValueTask CopyInputToAsync(
        ProviderWorkItem work, Stream destination, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var content = JsonContent.Create(ReceiptFor(work), PullJson.Default.ClaimReceipt);
        using var response = await SendAsync("input", content, cancellationToken);
        if (response.StatusCode != HttpStatusCode.OK) throw ExecutionFailed();
        if (response.Content.Headers.ContentLength is long size && size != work.Input.SizeBytes)
            throw ProviderPayloadVerifier.InvalidOutput();
        using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        await ProviderPayloadVerifier.CopyAsync(stream, destination, work.Input, cancellationToken);
    }

    /// <inheritdoc />
    public async ValueTask CompleteAsync(
        ProviderWorkItem work, Stream result, ProviderPayloadReference reference, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(result);
        if (!ProviderPayloadVerifier.IsValid(reference)) throw InvalidRequest();
        using var content = new MultipartFormDataContent();
        content.Add(JsonContent.Create(new CompletionRequest(ReceiptFor(work), reference), PullJson.Default.CompletionRequest), "metadata");
        content.Add(new VerifiedPayloadContent(result, reference), "payload", "payload");
        using var response = await SendAsync("complete", content, cancellationToken);
        if (response.StatusCode != HttpStatusCode.NoContent) throw ExecutionFailed();
    }

    /// <inheritdoc />
    public async ValueTask FailAsync(ProviderWorkItem work, string errorCode, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (errorCode is not (ProviderErrorCodes.NotImplemented or ProviderErrorCodes.ModelNotRegistered or
            ProviderErrorCodes.UnsupportedModality or ProviderErrorCodes.UnsupportedFormat or ProviderErrorCodes.UnsupportedOption or
            ProviderErrorCodes.InputLimitExceeded or ProviderErrorCodes.InvalidRequest or ProviderErrorCodes.InvalidOutput or
            ProviderErrorCodes.NoCandidateNode or ProviderErrorCodes.ExecutionFailed))
            throw InvalidRequest();
        using var content = JsonContent.Create(new FailureRequest(ReceiptFor(work), errorCode), PullJson.Default.FailureRequest);
        using var response = await SendAsync("fail", content, cancellationToken);
        if (response.StatusCode != HttpStatusCode.NoContent) throw ExecutionFailed();
    }

    private async Task<HttpResponseMessage> SendAsync(string action, HttpContent content, CancellationToken ct)
    {
        try
        {
            return await connection.SendAsync(centralServerId, HttpMethod.Post, Prefix + action, content, cancellationToken: ct);
        }
        catch (HttpRequestException)
        {
            ct.ThrowIfCancellationRequested();
            throw ExecutionFailed();
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            throw ExecutionFailed();
        }
    }

    private static ClaimReceipt ReceiptFor(ProviderWorkItem work)
    {
        ArgumentNullException.ThrowIfNull(work);
        if (string.IsNullOrWhiteSpace(work.JobId) || string.IsNullOrWhiteSpace(work.ClaimId)) throw InvalidRequest();
        return new(work.JobId, work.ClaimId);
    }

    private static ProviderOperationException ExecutionFailed() => new(
        new ExecutionError(ProviderErrorCodes.ExecutionFailed, "The central provider work API did not complete the operation."));

    private static ProviderOperationException InvalidRequest() => new(
        new ExecutionError(ProviderErrorCodes.InvalidRequest, "The provider work request is invalid."));

    private sealed class VerifiedPayloadContent(Stream data, ProviderPayloadReference reference) : HttpContent
    {
        protected override bool TryComputeLength(out long length)
        {
            length = reference.SizeBytes;
            return true;
        }

        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) =>
            SerializeToStreamAsync(stream, context, CancellationToken.None);

        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context, CancellationToken cancellationToken) =>
            ProviderPayloadVerifier.CopyAsync(data, stream, reference, cancellationToken).AsTask();
    }
}

internal sealed record ClaimRequest(string NodeId);
internal sealed record ClaimReceipt(string JobId, string ClaimId);
internal sealed record CompletionRequest(ClaimReceipt Claim, ProviderPayloadReference Result);
internal sealed record FailureRequest(ClaimReceipt Claim, string ErrorCode);

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower)]
[JsonSerializable(typeof(ProviderWorkItem))]
[JsonSerializable(typeof(ClaimRequest))]
[JsonSerializable(typeof(ClaimReceipt))]
[JsonSerializable(typeof(CompletionRequest))]
[JsonSerializable(typeof(FailureRequest))]
internal sealed partial class PullJson : JsonSerializerContext;
