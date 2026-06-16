namespace AIKernel.Providers.Substrate;

/// <summary>
/// [EN] Deterministic provider router for capability-based lookup.
/// [JA] capability-based lookup 向けの deterministic Provider router です。
/// </summary>
public sealed class ProviderRouter
{
    private readonly ProviderRegistry _registry;

    /// <summary>
    /// [EN] Initializes a provider router with a registry.
    /// [JA] registry を指定して Provider router を初期化します。
    /// </summary>
    /// <param name="registry">EN:  JA: registry パラメーターです。
    /// [EN] Provider registry.
    /// [JA] Provider registry です。
    /// </param>
    public ProviderRouter(ProviderRegistry registry)
    {
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
    }

    /// <summary>
    /// [EN] Resolves one provider for the supplied policy.
    /// [JA] 指定された policy に対して一つの Provider を解決します。
    /// </summary>
    /// <param name="policy">EN:  JA: policy パラメーターです。
    /// [EN] Provider resolution policy.
    /// [JA] Provider resolution policy です。
    /// </param>
    /// <returns>EN:  JA: 結果を返します。
    /// [EN] Structured provider resolution result.
    /// [JA] 構造化された Provider resolution result です。
    /// </returns>
    public ProviderResolutionResult Resolve(ProviderResolutionPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(policy);

        if (string.IsNullOrWhiteSpace(policy.RequiredCapability))
        {
            return Failure(
                "PROVIDER_CAPABILITY_REQUIRED",
                "Provider resolution requires a capability.",
                new MissingProviderResult
                {
                    RequiredCapability = policy.RequiredCapability,
                    PreferredProviderId = policy.PreferredProviderId,
                    RequiredTags = Normalize(policy.RequiredTags)
                });
        }

        var requiredCapability = policy.RequiredCapability.Trim();
        var requiredTags = Normalize(policy.RequiredTags);
        var providers = _registry.List();
        var candidates = CreateCandidates(policy, providers, requiredCapability, requiredTags)
            .OrderBy(candidate => candidate.SortKey.ProviderPriority)
            .ThenBy(candidate => candidate.SortKey.AvailabilityRank)
            .ThenBy(candidate => candidate.SortKey.ExactMatchRank)
            .ThenBy(candidate => candidate.SortKey.BackendPreferenceRank)
            .ThenBy(candidate => candidate.SortKey.ProviderId, StringComparer.Ordinal)
            .ThenBy(candidate => candidate.SortKey.ProviderVersion, StringComparer.Ordinal)
            .ThenBy(candidate => candidate.SortKey.ManifestPath, StringComparer.Ordinal)
            .ThenBy(candidate => candidate.SortKey.CapabilitySpecificity)
            .ThenBy(candidate => candidate.SortKey.BackendRank)
            .ThenBy(candidate => candidate.SortKey.BackendName, StringComparer.Ordinal)
            .ToArray();

        if (candidates.Length > 0)
        {
            var best = candidates[0];
            var duplicateCandidates = candidates
                .Where(candidate => candidate.SortKey == best.SortKey)
                .Select(candidate => candidate.Provider)
                .ToArray();

            if (duplicateCandidates.Length > 1)
            {
                return Duplicate(requiredCapability, duplicateCandidates);
            }

            return Success(
                best.Provider,
                best.Backend,
                candidates.Length > 1
                    ?
                    [
                        new ProviderDiagnostic
                        {
                            Code = "PROVIDER_DETERMINISTIC_SELECTION",
                            Message = "Provider router selected the highest-ranked deterministic candidate.",
                            Severity = "Information",
                            Source = best.Provider.Source,
                            Metadata = new Dictionary<string, string>(StringComparer.Ordinal)
                            {
                                ["requiredCapability"] = requiredCapability,
                                ["candidateCount"] = candidates.Length.ToString(System.Globalization.CultureInfo.InvariantCulture),
                                ["providerId"] = best.Provider.ProviderId,
                                ["backend"] = best.Backend?.BackendName ?? string.Empty
                            }
                        }
                    ]
                    : []);
        }

        if (policy.Fallback.Enabled)
        {
            var fallback = ResolveFallback(policy.Fallback, providers, requiredTags);
            if (fallback is not null)
            {
                return Success(
                    fallback,
                    SelectBackend(fallback, policy, requiredCapability)?.Selection,
                    [
                        new ProviderDiagnostic
                        {
                            Code = "PROVIDER_FALLBACK_USED",
                            Message = "Provider router selected an explicit deterministic fallback provider.",
                            Severity = "Warning",
                            Source = fallback.Source,
                            Metadata = new Dictionary<string, string>(StringComparer.Ordinal)
                            {
                                ["requiredCapability"] = requiredCapability
                            }
                        }
                    ]);
            }
        }

        return Failure(
            "PROVIDER_NOT_FOUND",
            "No provider matched the requested capability.",
            new MissingProviderResult
            {
                RequiredCapability = requiredCapability,
                PreferredProviderId = policy.PreferredProviderId,
                RequiredTags = requiredTags
            });
    }

    /// <summary>
    /// [EN] Resolves one provider for the supplied deterministic request.
    /// [JA] 指定された deterministic request に対して一つの Provider を解決します。
    /// </summary>
    /// <param name="request">EN:  JA: request パラメーターです。
    /// [EN] Provider resolution request.
    /// [JA] Provider resolution request です。
    /// </param>
    /// <returns>EN:  JA: 結果を返します。
    /// [EN] Structured provider resolution result.
    /// [JA] 構造化された Provider resolution result です。
    /// </returns>
    public ProviderResolutionResult Resolve(ProviderResolutionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var policy = request.Mode == ProviderResolutionMode.Strict
            ? request.Policy with { Fallback = new DeterministicFallbackPolicy() }
            : request.Policy;

        return Resolve(policy);
    }

    /// <summary>
    /// [EN] Returns the deterministic availability probe order for a resolution policy.
    /// [JA] resolution policy に対する deterministic availability probe order を返します。
    /// </summary>
    /// <param name="policy">EN:  JA: policy パラメーターです。
    /// [EN] Provider resolution policy.
    /// [JA] Provider resolution policy です。
    /// </param>
    /// <returns>EN:  JA: 結果を返します。
    /// [EN] Deterministically ordered provider/backend probe candidates.
    /// [JA] deterministic order の Provider/backend probe candidate です。
    /// </returns>
    public IReadOnlyList<ProviderProbeCandidate> GetAvailabilityProbeOrder(
        ProviderResolutionPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(policy);
        var requiredCapability = policy.RequiredCapability.Trim();
        var requiredTags = Normalize(policy.RequiredTags);

        return CreateCandidates(policy, _registry.List(), requiredCapability, requiredTags)
            .OrderBy(candidate => candidate.SortKey.ProviderPriority)
            .ThenBy(candidate => candidate.SortKey.AvailabilityRank)
            .ThenBy(candidate => candidate.SortKey.ExactMatchRank)
            .ThenBy(candidate => candidate.SortKey.BackendPreferenceRank)
            .ThenBy(candidate => candidate.SortKey.ProviderId, StringComparer.Ordinal)
            .ThenBy(candidate => candidate.SortKey.ProviderVersion, StringComparer.Ordinal)
            .ThenBy(candidate => candidate.SortKey.ManifestPath, StringComparer.Ordinal)
            .ThenBy(candidate => candidate.SortKey.CapabilitySpecificity)
            .ThenBy(candidate => candidate.SortKey.BackendRank)
            .ThenBy(candidate => candidate.SortKey.BackendName, StringComparer.Ordinal)
            .Select((candidate, index) => new ProviderProbeCandidate
            {
                Provider = candidate.Provider,
                Backend = candidate.Backend,
                Ordinal = index
            })
            .ToArray();
    }

    private static ProviderResolutionResult Success(
        ProviderManifestDescriptor provider,
        ProviderBackendSelection? backend,
        IReadOnlyList<ProviderDiagnostic> diagnostics)
        => new()
        {
            Succeeded = true,
            Provider = provider,
            Backend = backend,
            Diagnostics = diagnostics
        };

    private static ProviderResolutionResult Failure(
        string code,
        string message,
        MissingProviderResult missingProvider)
        => new()
        {
            Succeeded = false,
            ErrorCode = code,
            ErrorMessage = message,
            MissingProvider = missingProvider,
            Diagnostics =
            [
                new ProviderDiagnostic
                {
                    Code = code,
                    Message = message,
                    Severity = "Error",
                    Metadata = new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["requiredCapability"] = missingProvider.RequiredCapability
                    }
                }
            ]
        };

    private static ProviderResolutionResult Duplicate(
        string requiredCapability,
        IReadOnlyList<ProviderManifestDescriptor> candidates)
    {
        var duplicate = new DuplicateProviderDiagnostic
        {
            RequiredCapability = requiredCapability,
            ProviderIds = candidates
                .Select(provider => provider.ProviderId)
                .Order(StringComparer.Ordinal)
                .ToArray()
        };

        return new ProviderResolutionResult
        {
            Succeeded = false,
            ErrorCode = "PROVIDER_DUPLICATE",
            ErrorMessage = "Multiple providers matched the requested capability.",
            DuplicateProvider = duplicate,
            Diagnostics =
            [
                new ProviderDiagnostic
                {
                    Code = "PROVIDER_DUPLICATE",
                    Message = "Multiple providers matched the requested capability.",
                    Severity = "Error",
                    Metadata = new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["requiredCapability"] = requiredCapability,
                        ["providerIds"] = string.Join(",", duplicate.ProviderIds)
                    }
                }
            ]
        };
    }

    private static ProviderManifestDescriptor? ResolveFallback(
        DeterministicFallbackPolicy fallback,
        IReadOnlyList<ProviderManifestDescriptor> providers,
        IReadOnlyList<string> requiredTags)
    {
        if (fallback.ProviderIds.Count > 0)
        {
            foreach (var providerId in fallback.ProviderIds.Where(value => !string.IsNullOrWhiteSpace(value)))
            {
                var provider = providers.FirstOrDefault(item =>
                    string.Equals(item.ProviderId, providerId, StringComparison.Ordinal) &&
                    HasTags(item, requiredTags));
                if (provider is not null)
                {
                    return provider;
                }
            }

            return null;
        }

        return providers
            .Where(provider => HasTags(provider, requiredTags))
            .OrderBy(provider => provider.ProviderId, StringComparer.Ordinal)
            .FirstOrDefault();
    }

    private static IReadOnlyList<RouteCandidate> CreateCandidates(
        ProviderResolutionPolicy policy,
        IReadOnlyList<ProviderManifestDescriptor> providers,
        string requiredCapability,
        IReadOnlyList<string> requiredTags)
        => providers
            .Where(provider => MatchesPreferredProvider(provider, policy.PreferredProviderId))
            .Where(provider => HasCapability(provider, requiredCapability))
            .Where(provider => HasTags(provider, requiredTags))
            .Select(provider => CreateCandidate(provider, policy, requiredCapability))
            .Where(candidate => candidate is not null)
            .Select(candidate => candidate!)
            .ToArray();

    private static RouteCandidate? CreateCandidate(
        ProviderManifestDescriptor provider,
        ProviderResolutionPolicy policy,
        string requiredCapability)
    {
        var backend = SelectBackend(provider, policy, requiredCapability);
        if (backend is null)
        {
            return null;
        }

        var capabilitySpecificity = CapabilitySpecificity(provider, backend.Descriptor, requiredCapability);
        var sortKey = new RouteSortKey(
            provider.Priority,
            AvailabilityRank(provider, backend.Descriptor),
            ExactMatchRank(provider, backend.Descriptor, requiredCapability),
            backend.PreferenceRank,
            provider.ProviderId,
            provider.Version,
            provider.Source ?? string.Empty,
            capabilitySpecificity,
            backend.Selection.EffectiveRank,
            backend.Selection.BackendName);

        return new RouteCandidate(provider, backend.Selection, sortKey);
    }

    private static BackendCandidate? SelectBackend(
        ProviderManifestDescriptor provider,
        ProviderResolutionPolicy policy,
        string requiredCapability)
    {
        var descriptors = BackendDescriptorsOrImplicit(provider);
        var matchingBackends = descriptors
            .Where(backend => HasBackendCapability(backend, requiredCapability))
            .Select(backend => new BackendCandidate(
                backend,
                CreateBackendSelection(backend, provider),
                BackendPreferenceRank(backend, policy.RuntimeHints)))
            .OrderBy(candidate => candidate.PreferenceRank)
            .ThenBy(candidate => candidate.Selection.EffectiveRank)
            .ThenBy(candidate => candidate.Selection.BackendName, StringComparer.Ordinal)
            .ToArray();

        return matchingBackends.FirstOrDefault();
    }

    private static IReadOnlyList<ProviderBackendDescriptor> BackendDescriptorsOrImplicit(
        ProviderManifestDescriptor provider)
    {
        if (provider.BackendDescriptors.Count > 0)
        {
            return provider.BackendDescriptors;
        }

        var metadata = new ProviderManifestMergePolicy().MergeMetadata(provider);
        var backendName = ReadMetadata(metadata, "backend") ??
            ReadMetadata(metadata, "backendName") ??
            string.Empty;
        var backendKind = ReadMetadata(metadata, "backendKind") ??
            ReadMetadata(metadata, "kind") ??
            string.Empty;

        return
        [
            new ProviderBackendDescriptor
            {
                BackendName = backendName,
                Kind = backendKind,
                Rank = 0,
                Capabilities = provider.Capabilities,
                Tags = provider.Tags,
                Metadata = metadata
            }
        ];
    }

    private static ProviderBackendSelection CreateBackendSelection(
        ProviderBackendDescriptor backend,
        ProviderManifestDescriptor provider)
        => new()
        {
            BackendName = backend.BackendName,
            Kind = backend.Kind,
            EffectiveRank = backend.Rank,
            Metadata = backend.Metadata.Count > 0
                ? backend.Metadata
                : new ProviderManifestMergePolicy().MergeMetadata(provider)
        };

    private static int BackendPreferenceRank(
        ProviderBackendDescriptor backend,
        ProviderRuntimeHints hints)
    {
        if (!string.IsNullOrWhiteSpace(hints.Backend) &&
            string.Equals(backend.BackendName, hints.Backend, StringComparison.Ordinal))
        {
            return -10_000;
        }

        var explicitIndex = hints.BackendPreferenceOrder
            .Select((name, index) => new { name, index })
            .FirstOrDefault(item => string.Equals(item.name, backend.BackendName, StringComparison.Ordinal));
        if (explicitIndex is not null)
        {
            return -1_000 + explicitIndex.index;
        }

        if (hints.PreferLocalBackend)
        {
            if (string.Equals(backend.Kind, "local", StringComparison.OrdinalIgnoreCase))
            {
                return -100;
            }

            if (string.Equals(backend.Kind, "remote", StringComparison.OrdinalIgnoreCase))
            {
                return 100;
            }
        }

        return 0;
    }

    private static int CapabilitySpecificity(
        ProviderManifestDescriptor provider,
        ProviderBackendDescriptor backend,
        string requiredCapability)
    {
        var capabilities = backend.Capabilities.Count > 0
            ? backend.Capabilities
            : provider.Capabilities;
        var exact = capabilities.Count(capability =>
            string.Equals(capability, requiredCapability, StringComparison.OrdinalIgnoreCase));

        return exact > 0
            ? capabilities.Count
            : int.MaxValue;
    }

    private static int AvailabilityRank(
        ProviderManifestDescriptor provider,
        ProviderBackendDescriptor backend)
    {
        var availability = ReadMetadata(backend.Metadata, "availability") ??
            ReadMetadata(provider.Metadata, "availability") ??
            "available";

        return string.Equals(availability, "available", StringComparison.OrdinalIgnoreCase)
            ? 0
            : 1;
    }

    private static int ExactMatchRank(
        ProviderManifestDescriptor provider,
        ProviderBackendDescriptor backend,
        string requiredCapability)
    {
        var capabilities = backend.Capabilities.Count > 0
            ? backend.Capabilities
            : provider.Capabilities;

        return capabilities.Contains(requiredCapability, StringComparer.OrdinalIgnoreCase)
            ? 0
            : 1;
    }

    private static bool HasBackendCapability(
        ProviderBackendDescriptor backend,
        string requiredCapability)
        => backend.Capabilities.Count == 0 ||
           backend.Capabilities.Contains(requiredCapability, StringComparer.OrdinalIgnoreCase);

    private static bool MatchesPreferredProvider(
        ProviderManifestDescriptor provider,
        string? preferredProviderId)
        => string.IsNullOrWhiteSpace(preferredProviderId) ||
           string.Equals(provider.ProviderId, preferredProviderId, StringComparison.Ordinal);

    private static bool HasCapability(
        ProviderManifestDescriptor provider,
        string requiredCapability)
        => provider.Capabilities.Contains(requiredCapability, StringComparer.OrdinalIgnoreCase);

    private static bool HasTags(
        ProviderManifestDescriptor provider,
        IReadOnlyList<string> requiredTags)
        => requiredTags.Count == 0 ||
           requiredTags.All(tag => provider.Tags.Contains(tag, StringComparer.OrdinalIgnoreCase));

    private static IReadOnlyList<string> Normalize(IEnumerable<string> values)
        => values
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.Ordinal)
            .ToArray();

    private static string? ReadMetadata(
        IReadOnlyDictionary<string, string> metadata,
        string key)
        => metadata.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value
            : null;

    private sealed record RouteCandidate(
        ProviderManifestDescriptor Provider,
        ProviderBackendSelection? Backend,
        RouteSortKey SortKey);

    private sealed record BackendCandidate(
        ProviderBackendDescriptor Descriptor,
        ProviderBackendSelection Selection,
        int PreferenceRank);

    private sealed record RouteSortKey(
        int ProviderPriority,
        int AvailabilityRank,
        int ExactMatchRank,
        int BackendPreferenceRank,
        string ProviderId,
        string ProviderVersion,
        string ManifestPath,
        int CapabilitySpecificity,
        int BackendRank,
        string BackendName);
}
