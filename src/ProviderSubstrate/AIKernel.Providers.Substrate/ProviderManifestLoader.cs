namespace AIKernel.Providers.Substrate;

using System.Text.Json;

/// <summary>
/// [EN] Loads provider manifests from JSON without taking provider SDK dependencies.
/// [JA] Provider SDK 依存を持たずに JSON から Provider manifest を load します。
/// </summary>
public sealed class ProviderManifestLoader : IProviderManifestLoader
{
    private static readonly JsonDocumentOptions DocumentOptions = new()
    {
        AllowTrailingCommas = true,
        CommentHandling = JsonCommentHandling.Skip
    };

    /// <summary>
    /// [EN] Loads a provider manifest from JSON text.
    /// [JA] JSON text から Provider manifest を load します。
    /// </summary>
    /// <param name="json">
    /// [EN] Manifest JSON text.
    /// [JA] manifest JSON text です。
    /// </param>
    /// <param name="source">
    /// [EN] Optional source label.
    /// [JA] 任意の source label です。
    /// </param>
    /// <returns>
    /// [EN] Structured load result.
    /// [JA] 構造化された load result です。
    /// </returns>
    public ProviderManifestLoadResult LoadJson(string json, string? source = null)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return Failure(
                "PROVIDER_MANIFEST_EMPTY",
                "Provider manifest JSON is empty.",
                source);
        }

        try
        {
            using var document = JsonDocument.Parse(json, DocumentOptions);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return Failure(
                    "PROVIDER_MANIFEST_ROOT_INVALID",
                    "Provider manifest root must be a JSON object.",
                    source);
            }

            var root = document.RootElement;
            var metadata = ReadStringMap(root, "metadata");
            var backendMetadata = ReadStringMap(root, "backendMetadata");
            var vendorMetadata = ReadStringMap(root, "vendorMetadata");
            var extensionJson = ReadExtensionJson(root);
            var manifestVersion = ReadNullableString(root, "manifestVersion") ??
                ReadNullableString(root, "manifest_version") ??
                ReadNullableString(root, "schemaVersion") ??
                ReadNullableString(root, "schema_version") ??
                "1.0";
            var descriptor = new ProviderManifestDescriptor
            {
                SchemaVersion = manifestVersion,
                ManifestVersion = manifestVersion,
                ProviderId = ReadString(root, "providerId"),
                Name = ReadNullableString(root, "providerName") ?? ReadString(root, "name"),
                Version = ReadNullableString(root, "providerVersion") ?? ReadString(root, "version"),
                AssemblyName = ReadNullableString(root, "assemblyName") ?? ReadString(root, "assembly"),
                PackageId = ReadNullableString(root, "packageId"),
                ProviderType = ReadNullableString(root, "providerType") ??
                    ReadMapValue(backendMetadata, "providerType") ??
                    ReadMapValue(metadata, "providerType"),
                InvokerType = ReadNullableString(root, "invokerType") ??
                    ReadMapValue(backendMetadata, "invokerType") ??
                    ReadMapValue(metadata, "invokerType"),
                Capabilities = Normalize(ReadStringArray(root, "capabilities")),
                Priority = ReadInt(root, "priority"),
                Tags = Normalize(ReadStringArray(root, "tags")),
                Metadata = metadata,
                BackendMetadata = backendMetadata,
                BackendDescriptors = ReadBackendDescriptors(root),
                Dependencies = ReadDependencyDescriptors(root),
                Compatibility = ReadCompatibility(root),
                VendorMetadata = vendorMetadata,
                CliHints = ReadCliHints(root),
                ExtensionJson = extensionJson,
                Source = source
            };

            return new ProviderManifestLoadResult
            {
                Succeeded = true,
                Descriptor = descriptor
            };
        }
        catch (JsonException exception)
        {
            return Failure(
                "PROVIDER_MANIFEST_JSON_INVALID",
                exception.Message,
                source);
        }
    }

    /// <summary>
    /// [EN] Loads a provider manifest from a file.
    /// [JA] file から Provider manifest を load します。
    /// </summary>
    /// <param name="path">
    /// [EN] Manifest file path.
    /// [JA] manifest file path です。
    /// </param>
    /// <param name="cancellationToken">
    /// [EN] Cancellation token.
    /// [JA] cancellation token です。
    /// </param>
    /// <returns>
    /// [EN] Structured load result.
    /// [JA] 構造化された load result です。
    /// </returns>
    public async ValueTask<ProviderManifestLoadResult> LoadFileAsync(
        string path,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return Failure(
                "PROVIDER_MANIFEST_PATH_EMPTY",
                "Provider manifest path is empty.",
                path);
        }

        try
        {
            var json = await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);
            return LoadJson(json, path);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return Failure(
                "PROVIDER_MANIFEST_FILE_READ_FAILED",
                exception.Message,
                path);
        }
    }

    /// <summary>
    /// [EN] Loads a provider manifest from a stream.
    /// [JA] stream から Provider manifest を load します。
    /// </summary>
    /// <param name="stream">
    /// [EN] Manifest stream.
    /// [JA] manifest stream です。
    /// </param>
    /// <param name="source">
    /// [EN] Optional source label.
    /// [JA] 任意の source label です。
    /// </param>
    /// <param name="cancellationToken">
    /// [EN] Cancellation token.
    /// [JA] cancellation token です。
    /// </param>
    /// <returns>
    /// [EN] Structured load result.
    /// [JA] 構造化された load result です。
    /// </returns>
    public async ValueTask<ProviderManifestLoadResult> LoadStreamAsync(
        Stream stream,
        string? source,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(stream);

        try
        {
            using var reader = new StreamReader(stream, leaveOpen: true);
            var json = await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
            return LoadJson(json, source);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (IOException exception)
        {
            return Failure(
                "PROVIDER_MANIFEST_STREAM_READ_FAILED",
                exception.Message,
                source);
        }
    }

    /// <inheritdoc />
    public ValueTask<ProviderManifestLoadResult> LoadAsync(
        ProviderManifestLoadRequest request,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(request);

        if (!string.IsNullOrWhiteSpace(request.Json))
        {
            return ValueTask.FromResult(LoadJson(request.Json, request.Source));
        }

        if (!string.IsNullOrWhiteSpace(request.Path))
        {
            return LoadFileAsync(request.Path, cancellationToken);
        }

        if (request.Stream is not null)
        {
            return LoadStreamAsync(request.Stream, request.Source, cancellationToken);
        }

        return ValueTask.FromResult(Failure(
            "PROVIDER_MANIFEST_LOAD_SOURCE_MISSING",
            "Provider manifest load request must include JSON, path, or stream.",
            request.Source));
    }

    private static ProviderManifestLoadResult Failure(
        string code,
        string message,
        string? source)
        => new()
        {
            Succeeded = false,
            ErrorCode = code,
            ErrorMessage = message,
            Diagnostics =
            [
                new ProviderDiagnostic
                {
                    Code = code,
                    Message = message,
                    Severity = "Error",
                    Source = source
                }
            ]
        };

    private static string ReadString(JsonElement root, string propertyName)
        => root.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : string.Empty;

    private static string? ReadNullableString(JsonElement root, string propertyName)
        => root.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static IReadOnlyList<string> ReadStringArray(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var value) || value.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return value
            .EnumerateArray()
            .Where(item => item.ValueKind == JsonValueKind.String)
            .Select(item => item.GetString() ?? string.Empty)
            .ToArray();
    }

    private static IReadOnlyDictionary<string, string> ReadStringMap(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var value) || value.ValueKind != JsonValueKind.Object)
        {
            return new Dictionary<string, string>(StringComparer.Ordinal);
        }

        return value
            .EnumerateObject()
            .Where(property => property.Value.ValueKind is JsonValueKind.String or JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False)
            .OrderBy(property => property.Name, StringComparer.Ordinal)
            .ToDictionary(
                property => property.Name,
                property => property.Value.ValueKind == JsonValueKind.String
                    ? property.Value.GetString() ?? string.Empty
                    : property.Value.GetRawText(),
                StringComparer.Ordinal);
    }

    private static IReadOnlyDictionary<string, string> ReadExtensionJson(JsonElement root)
    {
        string[] known =
        [
            "providerId",
            "schemaVersion",
            "schema_version",
            "manifestVersion",
            "manifest_version",
            "name",
            "providerName",
            "version",
            "providerVersion",
            "assembly",
            "assemblyName",
            "packageId",
            "providerType",
            "invokerType",
            "capabilities",
            "priority",
            "tags",
            "metadata",
            "backendMetadata",
            "backendDescriptors",
            "dependencies",
            "compatibility",
            "vendorMetadata",
            "cli"
        ];

        return root
            .EnumerateObject()
            .Where(property => !known.Contains(property.Name, StringComparer.Ordinal))
            .OrderBy(property => property.Name, StringComparer.Ordinal)
            .ToDictionary(
                property => property.Name,
                property => property.Value.GetRawText(),
                StringComparer.Ordinal);
    }

    private static IReadOnlyList<string> Normalize(IEnumerable<string> values)
        => values
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.Ordinal)
            .ToArray();

    private static int ReadInt(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var value))
        {
            return 0;
        }

        return value.ValueKind switch
        {
            JsonValueKind.Number when value.TryGetInt32(out var number) => number,
            JsonValueKind.String when int.TryParse(value.GetString(), out var number) => number,
            _ => 0
        };
    }

    private static string? ReadMapValue(
        IReadOnlyDictionary<string, string> values,
        string key)
        => values.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value
            : null;

    private static ProviderCliHints ReadCliHints(JsonElement root)
    {
        if (!root.TryGetProperty("cli", out var value) || value.ValueKind != JsonValueKind.Object)
        {
            return new ProviderCliHints();
        }

        return new ProviderCliHints
        {
            Command = ReadNullableString(value, "command"),
            Commands = Normalize(ReadStringArray(value, "commands")),
            DefaultOperation = ReadNullableString(value, "defaultOperation"),
            ConfigKeys = Normalize(ReadStringArray(value, "configKeys")),
            RequiredEnvironment = Normalize(ReadStringArray(value, "requiredEnvironment")),
            ExtensionJson = ReadCliExtensionJson(value)
        };
    }

    private static IReadOnlyList<ProviderBackendDescriptor> ReadBackendDescriptors(JsonElement root)
    {
        if (!root.TryGetProperty("backendDescriptors", out var value) || value.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return value
            .EnumerateArray()
            .Where(item => item.ValueKind == JsonValueKind.Object)
            .Select(item => new ProviderBackendDescriptor
            {
                BackendName = ReadString(item, "backend") is { Length: > 0 } backend
                    ? backend
                    : ReadString(item, "backendName"),
                Kind = ReadString(item, "kind"),
                Rank = ReadInt(item, "rank"),
                Capabilities = Normalize(ReadStringArray(item, "capabilities")),
                Tags = Normalize(ReadStringArray(item, "tags")),
                Metadata = ReadStringMap(item, "metadata")
            })
            .Where(item => !string.IsNullOrWhiteSpace(item.BackendName))
            .OrderBy(item => item.Rank)
            .ThenBy(item => item.BackendName, StringComparer.Ordinal)
            .ToArray();
    }

    private static IReadOnlyList<ProviderDependencyDescriptor> ReadDependencyDescriptors(JsonElement root)
    {
        if (!root.TryGetProperty("dependencies", out var value) || value.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return value
            .EnumerateArray()
            .Where(item => item.ValueKind == JsonValueKind.Object)
            .Select(item => new ProviderDependencyDescriptor
            {
                Id = ReadString(item, "id"),
                Kind = ReadString(item, "kind"),
                Version = ReadNullableString(item, "version"),
                Optional = ReadBool(item, "optional", fallback: true),
                Metadata = ReadStringMap(item, "metadata")
            })
            .Where(item => !string.IsNullOrWhiteSpace(item.Id))
            .OrderBy(item => item.Id, StringComparer.Ordinal)
            .ThenBy(item => item.Kind, StringComparer.Ordinal)
            .ToArray();
    }

    private static ProviderCompatibilityDescriptor ReadCompatibility(JsonElement root)
    {
        if (!root.TryGetProperty("compatibility", out var value) || value.ValueKind != JsonValueKind.Object)
        {
            return new ProviderCompatibilityDescriptor();
        }

        return new ProviderCompatibilityDescriptor
        {
            MinimumAIKernelVersion = ReadNullableString(value, "minimumAIKernelVersion"),
            MaximumAIKernelVersion = ReadNullableString(value, "maximumAIKernelVersion"),
            TargetFrameworks = Normalize(ReadStringArray(value, "targetFrameworks")),
            Metadata = ReadStringMap(value, "metadata")
        };
    }

    private static bool ReadBool(
        JsonElement root,
        string propertyName,
        bool fallback)
    {
        if (!root.TryGetProperty(propertyName, out var value))
        {
            return fallback;
        }

        return value.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.String when bool.TryParse(value.GetString(), out var parsed) => parsed,
            _ => fallback
        };
    }

    private static IReadOnlyDictionary<string, string> ReadCliExtensionJson(JsonElement root)
    {
        string[] known =
        [
            "command",
            "commands",
            "defaultOperation",
            "configKeys",
            "requiredEnvironment"
        ];

        return root
            .EnumerateObject()
            .Where(property => !known.Contains(property.Name, StringComparer.Ordinal))
            .OrderBy(property => property.Name, StringComparer.Ordinal)
            .ToDictionary(
                property => property.Name,
                property => property.Value.GetRawText(),
                StringComparer.Ordinal);
    }
}
