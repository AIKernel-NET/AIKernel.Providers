namespace AIKernel.Providers.Substrate;

/// <summary>
/// [EN] Loads provider manifests into a deterministic registry catalog.
/// [JA] Provider manifest を deterministic registry catalog へ load します。
/// </summary>
public sealed class ProviderManifestCatalog
{
    private readonly ProviderManifestLoader _loader;
    private readonly ProviderManifestValidator _validator;

    /// <summary>
    /// [EN] Initializes a provider manifest catalog with default loader and validator.
    /// [JA] default loader / validator で Provider manifest catalog を初期化します。
    /// </summary>
    public ProviderManifestCatalog()
        : this(new ProviderManifestLoader(), new ProviderManifestValidator())
    {
    }

    /// <summary>
    /// [EN] Initializes a provider manifest catalog.
    /// [JA] Provider manifest catalog を初期化します。
    /// </summary>
    /// <param name="loader">EN:  JA: loader パラメーターです。
    /// [EN] Provider manifest loader.
    /// [JA] Provider manifest loader です。
    /// </param>
    /// <param name="validator">EN:  JA: validator パラメーターです。
    /// [EN] Provider manifest validator.
    /// [JA] Provider manifest validator です。
    /// </param>
    public ProviderManifestCatalog(
        ProviderManifestLoader loader,
        ProviderManifestValidator validator)
    {
        _loader = loader ?? throw new ArgumentNullException(nameof(loader));
        _validator = validator ?? throw new ArgumentNullException(nameof(validator));
    }

    /// <summary>
    /// [EN] Loads manifest files into a deterministic provider registry.
    /// [JA] manifest file を deterministic Provider registry へ load します。
    /// </summary>
    /// <param name="paths">EN:  JA: paths パラメーターです。
    /// [EN] Manifest file paths.
    /// [JA] manifest file path です。
    /// </param>
    /// <param name="options">EN:  JA: options パラメーターです。
    /// [EN] Validation options.
    /// [JA] validation option です。
    /// </param>
    /// <param name="cancellationToken">EN:  JA: cancellationToken パラメーターです。
    /// [EN] Cancellation token.
    /// [JA] cancellation token です。
    /// </param>
    /// <returns>EN:  JA: 結果を返します。
    /// [EN] Structured catalog result.
    /// [JA] 構造化された catalog result です。
    /// </returns>
    public async ValueTask<ProviderManifestCatalogResult> LoadFilesAsync(
        IEnumerable<string> paths,
        ProviderManifestValidationOptions? options,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(paths);

        var diagnostics = new List<ProviderDiagnostic>();
        var registry = new ProviderRegistry(_validator);
        foreach (var path in paths
                     .Where(path => !string.IsNullOrWhiteSpace(path))
                     .Distinct(StringComparer.Ordinal)
                     .Order(StringComparer.Ordinal))
        {
            var load = await _loader.LoadFileAsync(path, cancellationToken).ConfigureAwait(false);
            diagnostics.AddRange(load.Diagnostics);
            if (!load.Succeeded)
            {
                continue;
            }

            var validation = _validator.Validate(load.Descriptor, options ?? new ProviderManifestValidationOptions());
            diagnostics.AddRange(validation.Diagnostics);
            if (!validation.Succeeded)
            {
                continue;
            }

            var registration = registry.Register(load.Descriptor);
            diagnostics.AddRange(registration.Diagnostics);
        }

        var providers = registry.List();
        diagnostics.AddRange(FindDuplicateProviderIds(providers));
        diagnostics.AddRange(FindDuplicateCapabilities(providers));
        var hasErrors = diagnostics.Any(diagnostic =>
            string.Equals(diagnostic.Severity, "Error", StringComparison.OrdinalIgnoreCase));

        return new ProviderManifestCatalogResult
        {
            Succeeded = !hasErrors,
            Providers = providers,
            ErrorCode = hasErrors ? "PROVIDER_MANIFEST_CATALOG_INVALID" : null,
            ErrorMessage = hasErrors ? "Provider manifest catalog contains errors." : null,
            Diagnostics = diagnostics
                .OrderBy(diagnostic => diagnostic.Code, StringComparer.Ordinal)
                .ThenBy(diagnostic => diagnostic.Source, StringComparer.Ordinal)
                .ToArray()
        };
    }

    private static IReadOnlyList<ProviderDiagnostic> FindDuplicateProviderIds(
        IReadOnlyList<ProviderManifestDescriptor> providers)
        => providers
            .GroupBy(provider => provider.ProviderId, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .Select(group => new ProviderDiagnostic
            {
                Code = "PROVIDER_MANIFEST_DUPLICATE_PROVIDER_ID",
                Message = "Provider manifest catalog contains duplicate provider identifiers.",
                Severity = "Error",
                Metadata = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["providerId"] = group.Key,
                    ["count"] = group.Count().ToString(System.Globalization.CultureInfo.InvariantCulture)
                }
            })
            .ToArray();

    private static IReadOnlyList<ProviderDiagnostic> FindDuplicateCapabilities(
        IReadOnlyList<ProviderManifestDescriptor> providers)
        => providers
            .SelectMany(provider => provider.Capabilities.Select(capability => new { provider.ProviderId, Capability = capability }))
            .GroupBy(item => item.Capability, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Select(item => item.ProviderId).Distinct(StringComparer.Ordinal).Count() > 1)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .Select(group => new ProviderDiagnostic
            {
                Code = "PROVIDER_MANIFEST_DUPLICATE_CAPABILITY",
                Message = "Provider manifest catalog contains a capability exposed by multiple providers.",
                Severity = "Warning",
                Metadata = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["capability"] = group.Key,
                    ["providerIds"] = string.Join(",", group.Select(item => item.ProviderId).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal))
                }
            })
            .ToArray();
}
