using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AIKernel.Providers.Tts;

internal static class TtsRuntimeClient
{
    private const int MaxHealthBytes = 16 * 1024;
    internal const int MaxWaveBytes = 16 * 1024 * 1024;

    private static readonly HttpClient Client = new(new SocketsHttpHandler
    {
        AllowAutoRedirect = false,
        UseCookies = false,
        UseProxy = false
    })
    {
        Timeout = System.Threading.Timeout.InfiniteTimeSpan
    };

    internal static Task<HttpResponseMessage> HealthAsync(Uri address, CancellationToken cancellationToken)
        => Client.GetAsync(new Uri(address, "health"), HttpCompletionOption.ResponseHeadersRead, cancellationToken);

    internal static async Task<HttpResponseMessage> SynthesizeAsync(
        Uri address, TtsRuntimeRequest payload, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(address, "synthesize"))
        {
            Content = new ByteArrayContent(JsonSerializer.SerializeToUtf8Bytes(payload, TtsJsonContext.Default.TtsRuntimeRequest))
        };
        request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        return await Client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);
    }

    internal static async Task<TtsRuntimeHealth?> ReadHealthAsync(
        HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var bytes = await ReadBytesAsync(response, MaxHealthBytes, cancellationToken).ConfigureAwait(false);
        return JsonSerializer.Deserialize(bytes, TtsJsonContext.Default.TtsRuntimeHealth);
    }

    internal static async Task<byte[]> ReadBytesAsync(
        HttpResponseMessage response, int limit, CancellationToken cancellationToken)
    {
        var length = response.Content.Headers.ContentLength;
        if (length > limit)
            throw new InvalidDataException("The TTS response exceeds the configured size limit.");

        using var buffer = new MemoryStream(length.HasValue ? (int)length.Value : 0);
        using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        var chunk = new byte[16 * 1024];
        while (true)
        {
            var read = await stream.ReadAsync(chunk, cancellationToken).ConfigureAwait(false);
            if (read == 0)
                return buffer.ToArray();

            if (buffer.Length + read > limit)
                throw new InvalidDataException("The TTS response exceeds the configured size limit.");

            buffer.Write(chunk, 0, read);
        }
    }

    internal static string? Header(HttpResponseMessage response, string name)
    {
        if (!response.Headers.TryGetValues(name, out var values))
            return null;

        using var headers = values.GetEnumerator();
        if (!headers.MoveNext())
            return null;
        var value = headers.Current;
        return headers.MoveNext() ? null : value;
    }
}

internal sealed record TtsRuntimeRequest(string Text, string Language, string Voice, double Speed);

internal sealed record TtsRuntimeHealth(string? Engine, string[]? Languages, string[]? Voices);

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(TtsRuntimeRequest))]
[JsonSerializable(typeof(TtsRuntimeHealth))]
internal sealed partial class TtsJsonContext : JsonSerializerContext;
