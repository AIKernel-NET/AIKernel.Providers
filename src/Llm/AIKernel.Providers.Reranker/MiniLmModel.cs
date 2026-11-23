using System.Security.Cryptography;
using AIKernel.Abstractions.Providers;
using AIKernel.Dtos.Execution;
using AIKernel.Dtos.Providers;
using Microsoft.ML.OnnxRuntime;
using Tokenizers.HuggingFace.Tokenizer;

namespace AIKernel.Providers.Reranker;

internal sealed class MiniLmModel : IDisposable
{
    internal const string ModelId = "cross-encoder/mmarco-mMiniLMv2-L12-H384-v1";
    internal const string Revision = "1427fd652930e4ba29e8149678df786c240d8825";
    internal const string WeightsSha256 = "3e9a03ed1e966f7c5288dd4230e3d6a9bf5e3a170a06f1f4241c5bca12c6487c";
    internal const string TokenizerSha256 = "62c24cdc13d4c9952d63718d6c9fa4c287974249e16b7ade6d5a85e7bbb75626";
    internal const int ContextLength = 512;
    internal const int PairSpecialTokens = 4;
    internal const int WeightsBytes = 470_883_696;
    internal const int TokenizerBytes = 17_082_660;
    internal const int HiddenSize = 384;
    internal const int AttentionHeads = 12;
    private const long BeginId = 0;
    private const long PadId = 1;
    private const long EndId = 2;
    private const string OutputName = "logits";

    private readonly Tokenizer tokenizer;
    private readonly InferenceSession session;

    internal MiniLmModel(string directory, int cpuThreads)
    {
        tokenizer = LoadTokenizer(directory);
        InferenceSession? opened = null;
        try
        {
            using var options = new SessionOptions { IntraOpNumThreads = cpuThreads, InterOpNumThreads = 1 };
            // Pass the verified buffer to ORT, including on systems where a path could be replaced.
            opened = new InferenceSession(ReadPinned(Path.Combine(directory, "model.onnx"), WeightsBytes, WeightsSha256), options);
            session = opened;
            if (session.InputMetadata.Count != 2 ||
                !IsTokenInput("input_ids") || !IsTokenInput("attention_mask") ||
                !session.OutputMetadata.TryGetValue(OutputName, out var output) ||
                output.ElementType != typeof(float) || output.Dimensions.Length != 2 || output.Dimensions[1] != 1)
                throw new InvalidDataException("Unexpected MiniLM ONNX graph metadata.");
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

    internal IReadOnlyList<uint> EncodeText(string text, int limit, string limitMessage)
    {
        var tokens = tokenizer.Encode(text, addSpecialTokens: false).Single().Ids;
        if (tokens.Count == 0)
            throw Error(ProviderErrorCodes.InvalidRequest, "The reranking text has no tokens.");
        if (tokens.Count > limit)
            throw Error(ProviderErrorCodes.InputLimitExceeded, limitMessage);
        return tokens;
    }

    internal static long[] CreatePair(IReadOnlyList<uint> query, IReadOnlyList<uint> candidate, int limit)
    {
        int length = query.Count + candidate.Count + PairSpecialTokens;
        if (length > limit)
            throw Error(ProviderErrorCodes.InputLimitExceeded, "A query-candidate pair exceeds the configured token limit.");

        // The pinned XLM-R tokenizer's pair template is <s> query </s></s> candidate </s>.
        // Reusing these query tokens avoids tokenizing the same query for every candidate.
        var pair = new long[length];
        pair[0] = BeginId;
        for (int index = 0; index < query.Count; index++)
            pair[index + 1] = query[index];
        pair[query.Count + 1] = EndId;
        pair[query.Count + 2] = EndId;
        for (int index = 0; index < candidate.Count; index++)
            pair[query.Count + 3 + index] = candidate[index];
        pair[^1] = EndId;
        return pair;
    }

    internal float[] Score(ReadOnlySpan<long[]> pairs, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        int width = 0;
        foreach (var pair in pairs)
            width = Math.Max(width, pair.Length);

        long[] shape = [pairs.Length, width];
        int count = checked(pairs.Length * width);
        var ids = new long[count];
        Array.Fill(ids, PadId);
        var mask = new long[count];
        for (int row = 0; row < pairs.Length; row++)
        {
            pairs[row].CopyTo(ids, row * width);
            mask.AsSpan(row * width, pairs[row].Length).Fill(1);
        }

        using var input = OrtValue.CreateTensorValueFromMemory(ids, shape);
        using var attention = OrtValue.CreateTensorValueFromMemory(mask, shape);
        var inputs = new Dictionary<string, OrtValue>(StringComparer.Ordinal)
        {
            ["input_ids"] = input,
            ["attention_mask"] = attention
        };

        using var run = new RunOptions();
        using var registration = cancellationToken.Register(static state => ((RunOptions)state!).Terminate = true, run);
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            using var outputs = session.Run(run, inputs, [OutputName]);
            var logits = outputs[0].GetTensorDataAsSpan<float>();
            if (logits.Length != pairs.Length)
                throw Error(ProviderErrorCodes.InvalidOutput, "The reranker returned an unexpected number of logits.");
            foreach (float logit in logits)
                if (!float.IsFinite(logit))
                    throw Error(ProviderErrorCodes.InvalidOutput, "The reranker returned a non-finite logit.");
            cancellationToken.ThrowIfCancellationRequested();
            return logits.ToArray();
        }
        catch (OnnxRuntimeException)
        {
            cancellationToken.ThrowIfCancellationRequested();
            throw Error(ProviderErrorCodes.ExecutionFailed, "The reranker could not complete inference.");
        }
    }

    private static Tokenizer LoadTokenizer(string directory)
    {
        var data = ReadPinned(Path.Combine(directory, "tokenizer.json"), TokenizerBytes, TokenizerSha256);
        string path = Path.Combine(Path.GetTempPath(), "aikernel-reranker-" + Guid.NewGuid().ToString("N") + ".json");
        using var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read | FileShare.Delete,
            bufferSize: 64 * 1024, FileOptions.DeleteOnClose);
        file.Write(data);
        file.Flush();
        return Tokenizer.FromFile(path);
    }

    private static byte[] ReadPinned(string path, int size, string sha256)
    {
        using var file = File.OpenRead(path);
        if (file.Length != size)
            throw new InvalidDataException("A pinned reranker file has an unexpected size.");
        var bytes = GC.AllocateUninitializedArray<byte>(size);
        file.ReadExactly(bytes);
        if (!SHA256.HashData(bytes).AsSpan().SequenceEqual(Convert.FromHexString(sha256)))
            throw new InvalidDataException("A pinned reranker file does not match its checksum.");
        return bytes;
    }

    internal static ProviderOperationException Error(string code, string message) => new(new ExecutionError(code, message));

    public void Dispose()
    {
        session.Dispose();
        tokenizer.Dispose();
    }
}
