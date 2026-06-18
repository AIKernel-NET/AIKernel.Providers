namespace AIKernel.Providers.Substrate;

/// <summary>
/// [EN] Validates provider manifests using fail-closed structured diagnostics.
/// [JA] fail-closed な構造化 diagnostic で Provider manifest を検証します。
/// </summary>
public sealed class ProviderManifestValidator : IProviderManifestValidator
{
    /// <summary>
    /// [EN] Validates a loaded provider manifest descriptor.
    /// [JA] load 済み Provider manifest descriptor を検証します。
    /// </summary>
    /// <param name="descriptor">
    /// [EN] Provider manifest descriptor to validate.
    /// [JA] 検証対象の Provider manifest descriptor です。
    /// </param>
    /// <returns>
    /// [EN] Structured validation result.
    /// [JA] 構造化された validation result です。
    /// </returns>
    public ProviderManifestValidationResult Validate(ProviderManifestDescriptor? descriptor)
        => Validate(descriptor, new ProviderManifestValidationOptions());

    /// <summary>
    /// [EN] Validates a loaded provider manifest descriptor with strictness options.
    /// [JA] strictness option を指定して load 済み Provider manifest descriptor を検証します。
    /// </summary>
    /// <param name="descriptor">
    /// [EN] Provider manifest descriptor to validate.
    /// [JA] 検証対象の Provider manifest descriptor です。
    /// </param>
    /// <param name="options">
    /// [EN] Validation options.
    /// [JA] validation option です。
    /// </param>
    /// <returns>
    /// [EN] Structured validation result.
    /// [JA] 構造化された validation result です。
    /// </returns>
    public ProviderManifestValidationResult Validate(
        ProviderManifestDescriptor? descriptor,
        ProviderManifestValidationOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (descriptor is null)
        {
            return Failure(
                "PROVIDER_MANIFEST_DESCRIPTOR_MISSING",
                "Provider manifest descriptor is missing.",
                null);
        }

        var diagnostics = new List<ProviderDiagnostic>();
        var errors = new List<ProviderManifestValidationError>();
        if (options.RequireSchemaVersion)
        {
            AddRequiredDiagnostic(diagnostics, errors, descriptor.SchemaVersion, "schemaVersion", descriptor.Source);
        }

        AddRequiredDiagnostic(diagnostics, errors, descriptor.ProviderId, "providerId", descriptor.Source);
        AddRequiredDiagnostic(diagnostics, errors, descriptor.Name, "name", descriptor.Source);
        AddRequiredDiagnostic(diagnostics, errors, descriptor.Version, "version", descriptor.Source);
        AddRequiredDiagnostic(diagnostics, errors, descriptor.AssemblyName, "assembly", descriptor.Source);

        if (options.RequireCapabilities && descriptor.Capabilities.Count == 0)
        {
            errors.Add(CreateError(
                "PROVIDER_MANIFEST_CAPABILITIES_MISSING",
                "Provider manifest must declare at least one capability.",
                "capabilities",
                descriptor.Source));
            diagnostics.Add(new ProviderDiagnostic
            {
                Code = "PROVIDER_MANIFEST_CAPABILITIES_MISSING",
                Message = "Provider manifest must declare at least one capability.",
                Severity = "Error",
                Source = descriptor.Source
            });
        }

        if (diagnostics.Count > 0)
        {
            return new ProviderManifestValidationResult
            {
                Succeeded = false,
                ErrorCode = "PROVIDER_MANIFEST_INVALID",
                ErrorMessage = "Provider manifest validation failed.",
                Errors = errors,
                Diagnostics = diagnostics
            };
        }

        return new ProviderManifestValidationResult
        {
            Succeeded = true,
            Diagnostics =
            [
                new ProviderDiagnostic
                {
                    Code = "PROVIDER_MANIFEST_VALID",
                    Message = "Provider manifest validation succeeded.",
                    Severity = "Information",
                    Source = descriptor.Source
                }
            ]
        };
    }

    /// <summary>[EN] Documents this public package API member. [JA] ValidateAsync を取得します。</summary>
    /// <inheritdoc />
    public ValueTask<ProviderManifestValidationResult> ValidateAsync(
        ProviderManifestDescriptor descriptor,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(Validate(descriptor));
    }

    private static ProviderManifestValidationResult Failure(
        string code,
        string message,
        string? source)
        => new()
        {
            Succeeded = false,
            ErrorCode = code,
            ErrorMessage = message,
            Errors =
            [
                CreateError(code, message, null, source)
            ],
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

    private static void AddRequiredDiagnostic(
        List<ProviderDiagnostic> diagnostics,
        List<ProviderManifestValidationError> errors,
        string value,
        string fieldName,
        string? source)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        errors.Add(CreateError(
            "PROVIDER_MANIFEST_REQUIRED_FIELD_MISSING",
            $"Provider manifest is missing required field '{fieldName}'.",
            fieldName,
            source));
        diagnostics.Add(new ProviderDiagnostic
        {
            Code = "PROVIDER_MANIFEST_REQUIRED_FIELD_MISSING",
            Message = $"Provider manifest is missing required field '{fieldName}'.",
            Severity = "Error",
            Source = source,
            Metadata = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["field"] = fieldName
            }
        });
    }

    private static ProviderManifestValidationError CreateError(
        string code,
        string message,
        string? field,
        string? source)
        => new()
        {
            Code = code,
            Message = message,
            Field = field,
            Source = source,
            Metadata = field is null
                ? new Dictionary<string, string>(StringComparer.Ordinal)
                : new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["field"] = field
                }
        };
}
