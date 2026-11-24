using System.Globalization;
using AIKernel.Abstractions.Capabilities;
using AIKernel.Dtos.Capabilities;
using AIKernel.Kernel.Inference;

namespace AIKernel.Providers.LocalLlm;

/// <summary>Generates local replies through the AIKernel Kernel inference engine for a host-registered model.</summary>
public sealed class LocalLlmInvoker : ICapabilityModuleInvoker, IDisposable
{
    private const string ChatOperation = "chat.local";
    private const string ModelNotRegistered = "provider.model_not_registered";
    private readonly LocalLlmSettings _settings;
    private readonly string? _modelPath;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private DenseModelSession? _model;
    private bool _disposed;

    /// <summary>Uses the model registered through the AIKERNEL_LOCAL_LLM_MODEL_PATH environment variable.</summary>
    public LocalLlmInvoker() : this(new LocalLlmSettings
    {
        BonsaiModelPath = Environment.GetEnvironmentVariable("AIKERNEL_LOCAL_LLM_MODEL_PATH")
    })
    {
    }

    /// <summary>Uses host settings and opens the registered model on the first valid request.</summary>
    public LocalLlmInvoker(LocalLlmSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        if (settings.CpuThreads is < 1 or > 6 || settings.ContextTokens is < 64 or > 2048
            || settings.MaxPromptCharacters is < 1 or > 4096 || settings.MaxNewTokens is < 1 or > 256
            || settings.MaxNewTokens >= settings.ContextTokens || settings.SystemPrompt is null
            || settings.SystemPrompt.Length > 8192)
            throw new ArgumentException("The local LLM limits or system prompt are invalid.", nameof(settings));

        _settings = settings;
        _modelPath = string.IsNullOrWhiteSpace(settings.BonsaiModelPath) ? null : Path.GetFullPath(settings.BonsaiModelPath);
    }

    /// <inheritdoc />
    public async ValueTask<CapabilityInvocationResult> InvokeAsync(
        CapabilityInvocationRequest request, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(request);

        if (request.Operation == "embedding.local")
            return Failure(ModelNotRegistered, "Register an embedding model with AIKernel.Providers.Embedding.");
        if (request.Operation != ChatOperation)
            return Failure("LOCAL_LLM_OPERATION_NOT_SUPPORTED", "Use the chat.local operation.");
        if (_modelPath is null)
            return Failure(ModelNotRegistered, "The host has not registered a local chat model.");

        var arguments = request.Arguments;
        if (arguments.Keys.Any(key => key is not ("prompt" or "max_new_tokens")))
            return Failure("LOCAL_LLM_INVALID_ARGUMENT", "Only prompt and max_new_tokens are accepted.");
        if (!arguments.TryGetValue("prompt", out var prompt) || string.IsNullOrWhiteSpace(prompt)
            || prompt.Length > _settings.MaxPromptCharacters)
            return Failure("LOCAL_LLM_INVALID_ARGUMENT", "Provide a non-empty prompt within the configured limit.");
        int maxNewTokens = _settings.MaxNewTokens;
        if (arguments.TryGetValue("max_new_tokens", out var requested)
            && (!int.TryParse(requested, NumberStyles.None, CultureInfo.InvariantCulture, out maxNewTokens)
                || maxNewTokens < 1 || maxNewTokens > _settings.MaxNewTokens))
            return Failure("LOCAL_LLM_INVALID_ARGUMENT", "max_new_tokens exceeds the configured limit or is invalid.");

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                _model ??= LocalModelReader.Open(_modelPath, _settings, cancellationToken);
            }
            catch (Exception error) when (error is FileNotFoundException or DirectoryNotFoundException)
            {
                cancellationToken.ThrowIfCancellationRequested();
                return Failure(ModelNotRegistered, "The registered local chat model file is missing.");
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException
                or InvalidDataException or NotSupportedException or ArgumentException)
            {
                cancellationToken.ThrowIfCancellationRequested();
                return Failure("LOCAL_LLM_MODEL_UNAVAILABLE", "The registered local chat model could not be opened.");
            }

            CpuGeneration generation;
            try
            {
                generation = await _model.GenerateAsync(_settings.SystemPrompt, prompt, maxNewTokens, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (ArgumentException)
            {
                cancellationToken.ThrowIfCancellationRequested();
                return Failure("LOCAL_LLM_INVALID_ARGUMENT", "The prompt and reply exceed the model context budget.");
            }

            cancellationToken.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(generation.Text))
                return Failure("LOCAL_LLM_EMPTY_OUTPUT", "The model returned no reply text.");

            return LocalLlmResponse.Success(request, generation);
        }
        finally
        {
            _gate.Release();
        }

        CapabilityInvocationResult Failure(string code, string message)
            => new(request.InvocationId, request.CapabilityId, false, null, code, message, request.ReplayLogHash,
                new Dictionary<string, string>(StringComparer.Ordinal) { ["provider"] = "LocalLlmProvider" });
    }

    /// <summary>Waits for any active generation and releases the registered model.</summary>
    public void Dispose()
    {
        _gate.Wait();
        try
        {
            if (_disposed)
                return;

            _disposed = true;
            _model?.Dispose();
            _model = null;
        }
        finally
        {
            _gate.Release();
        }
    }
}
