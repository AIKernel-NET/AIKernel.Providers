using System.Security.Cryptography;
using AIKernel.Abstractions.Providers;
using AIKernel.Dtos.Execution;
using AIKernel.Dtos.Providers;
using AIKernel.Dtos.Providers.Embedding;
using Microsoft.ML.OnnxRuntime;
using Tokenizers.HuggingFace.Tokenizer;

namespace AIKernel.Providers.Embedding;

internal sealed class E5Model : IDisposable
{
    internal const string ModelId = "intfloat/multilingual-e5-small";
    internal const string Revision = "614241f622f53c4eeff9890bdc4f31cfecc418b3";
    internal const string WeightsSha256 = "ca456c06b3a9505ddfd9131408916dd79290368331e7d76bb621f1cba6bc8665";
    internal const string TokenizerSha256 = "0b44a9d7b51c3c62626640cda0e2c2f70fdacdc25bbbd68038369d14ebdf4c39";
    internal const int Dimensions = 384;
    internal const int ContextLength = 512;
    internal const int WeightsBytes = 470_268_510;
    private const int TokenizerBytes = 17_082_730;
    private const long PadId = 1;
    private const string OutputName = "last_hidden_state";

    private readonly Tokenizer tokenizer;
    private readonly InferenceSession session;
    private readonly bool requiresTokenTypes;

    internal E5Model(string directory, int cpuThreads)
    {
        tokenizer = LoadTokenizer(directory);
        InferenceSession? opened = null;
        try
        {
            using var options = new SessionOptions { IntraOpNumThreads = cpuThreads, InterOpNumThreads = 1 };
            // ORT sees the exact bytes that passed the hash check.
            opened = new InferenceSession(ReadPinned(Path.Combine(directory, "model.onnx"), WeightsBytes, WeightsSha256), options);
            session = opened;
            requiresTokenTypes = session.InputMetadata.ContainsKey("token_type_ids");
            if (session.InputMetadata.Count != (requiresTokenTypes ? 3 : 2) ||
                !IsTokenInput("input_ids") || !IsTokenInput("attention_mask") ||
                (requiresTokenTypes && !IsTokenInput("token_type_ids")) ||
                !session.OutputMetadata.TryGetValue(OutputName, out var output) ||
                output.ElementType != typeof(float) || output.Dimensions.Length != 3 || output.Dimensions[^1] != Dimensions)
                throw new InvalidDataException("Unexpected E5 ONNX graph metadata.");
        }
        catch
        {
            opened?.Dispose();
            tokenizer.Dispose();
            throw;
        }
    }

    private bool IsTokenInput(string name) =>
        session.InputMetadata.TryGetValue(name, out var input) && input.ElementType == typeof(long) && input.Dimensions.Length == 2;

    internal long[] Encode(string text, EmbeddingPurpose purpose, int maxTokens)
    {
        string prefix = purpose == EmbeddingPurpose.Document ? "passage: " : "query: ";
        var body = tokenizer.Encode(prefix + text, addSpecialTokens: true).Single().Ids;
        if (body.Count > maxTokens)
            throw Error(ProviderErrorCodes.InputLimitExceeded, "An embedding input exceeds the configured token limit.");

        var ids = new long[body.Count];
        for (var i = 0; i < body.Count; i++)
            ids[i] = body[i];
        return ids;
    }

    internal IReadOnlyList<EmbeddingVector> Run(long[][] batch, bool normalize, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        int width = batch.Max(ids => ids.Length);
        long[] shape = [batch.Length, width];
        int count = checked(batch.Length * width);
        var ids = new long[count];
        Array.Fill(ids, PadId);
        var mask = new long[count];
        for (int index = 0; index < batch.Length; index++)
        {
            batch[index].CopyTo(ids, index * width);
            mask.AsSpan(index * width, batch[index].Length).Fill(1);
        }

        using var input = OrtValue.CreateTensorValueFromMemory(ids, shape);
        using var attention = OrtValue.CreateTensorValueFromMemory(mask, shape);
        using var types = requiresTokenTypes ? OrtValue.CreateTensorValueFromMemory(new long[count], shape) : null;
        var inputs = new Dictionary<string, OrtValue>(StringComparer.Ordinal)
        {
            ["input_ids"] = input,
            ["attention_mask"] = attention
        };
        if (types is not null)
            inputs["token_type_ids"] = types;

        using var run = new RunOptions();
        using var registration = ct.Register(static state => ((RunOptions)state!).Terminate = true, run);
        ct.ThrowIfCancellationRequested();
        try
        {
            using var outputs = session.Run(run, inputs, [OutputName]);
            var tokens = outputs[0].GetTensorDataAsSpan<float>();
            if (tokens.Length != checked(count * Dimensions))
                throw Error(ProviderErrorCodes.InvalidOutput, "The embedding model returned an unexpected output shape.");
            var result = new EmbeddingVector[batch.Length];
            for (int index = 0; index < result.Length; index++)
            {
                ct.ThrowIfCancellationRequested();
                result[index] = new()
                {
                    Index = index,
                    Dense = Pool(tokens.Slice(index * width * Dimensions, batch[index].Length * Dimensions), batch[index].Length, normalize)
                };
            }
            ct.ThrowIfCancellationRequested();
            return Array.AsReadOnly(result);
        }
        catch (OnnxRuntimeException)
        {
            ct.ThrowIfCancellationRequested();
            throw Error(ProviderErrorCodes.ExecutionFailed, "The embedding model could not complete inference.");
        }
    }

    private static float[] Pool(ReadOnlySpan<float> tokens, int tokenCount, bool normalize)
    {
        var result = new float[Dimensions];
        for (int offset = 0; offset < tokens.Length; offset += Dimensions)
            for (int dim = 0; dim < Dimensions; dim++)
                result[dim] += tokens[offset + dim];

        double sumSquares = 0;
        for (var dim = 0; dim < Dimensions; dim++)
        {
            float mean = result[dim] / tokenCount;
            if (!float.IsFinite(mean))
                throw Error(ProviderErrorCodes.InvalidOutput, "The embedding model returned a non-finite value.");
            result[dim] = mean;
            sumSquares += (double)mean * mean;
        }
        if (sumSquares == 0)
            throw Error(ProviderErrorCodes.InvalidOutput, "The embedding model returned a zero vector.");
        if (normalize)
        {
            var norm = Math.Sqrt(sumSquares);
            for (var dim = 0; dim < Dimensions; dim++)
                result[dim] = (float)(result[dim] / norm);
        }
        return result;
    }

    private static Tokenizer LoadTokenizer(string directory)
    {
        var data = ReadPinned(Path.Combine(directory, "tokenizer.json"), TokenizerBytes, TokenizerSha256);
        // The native binding loads by path. Give it an exclusively created copy of the checked bytes.
        string path = Path.Combine(Path.GetTempPath(), "aikernel-e5-" + Guid.NewGuid().ToString("N") + ".json");
        using var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read | FileShare.Delete,
            bufferSize: 64 * 1024, FileOptions.DeleteOnClose);
        file.Write(data);
        file.Flush();
        return Tokenizer.FromFile(path);
    }

    private static byte[] ReadPinned(string path, int bytes, string sha256)
    {
        using var file = File.OpenRead(path);
        if (file.Length != bytes)
            throw new InvalidDataException("A pinned embedding file has an unexpected size.");
        var buffer = GC.AllocateUninitializedArray<byte>(bytes);
        file.ReadExactly(buffer);
        if (!SHA256.HashData(buffer).AsSpan().SequenceEqual(Convert.FromHexString(sha256)))
            throw new InvalidDataException("A pinned embedding file does not match its checksum.");
        return buffer;
    }

    internal static ProviderOperationException Error(string code, string message) => new(new ExecutionError(code, message));

    public void Dispose()
    {
        session.Dispose();
        tokenizer.Dispose();
    }
}
