namespace AIKernel.Providers.Substrate;

/// <summary>
/// [EN] Deterministic in-memory registry for provider manifest descriptors.
/// [JA] Provider manifest descriptor 向けの deterministic in-memory registry です。
/// </summary>
public sealed class ProviderRegistry
{
    private readonly List<ProviderManifestDescriptor> _providers = [];
    private readonly ProviderManifestValidator _validator;

    /// <summary>
    /// [EN] Initializes an empty provider registry.
    /// [JA] 空の Provider registry を初期化します。
    /// </summary>
    public ProviderRegistry()
        : this(new ProviderManifestValidator())
    {
    }

    /// <summary>
    /// [EN] Initializes a provider registry with a validator.
    /// [JA] validator を指定して Provider registry を初期化します。
    /// </summary>
    /// <param name="validator">EN:  JA: validator パラメーターです。
    /// [EN] Manifest validator.
    /// [JA] manifest validator です。
    /// </param>
    public ProviderRegistry(ProviderManifestValidator validator)
    {
        _validator = validator ?? throw new ArgumentNullException(nameof(validator));
    }

    /// <summary>
    /// [EN] Registers a provider manifest descriptor.
    /// [JA] Provider manifest descriptor を登録します。
    /// </summary>
    /// <param name="descriptor">EN:  JA: descriptor パラメーターです。
    /// [EN] Provider manifest descriptor.
    /// [JA] Provider manifest descriptor です。
    /// </param>
    /// <returns>EN:  JA: 結果を返します。
    /// [EN] Structured registration result.
    /// [JA] 構造化された registration result です。
    /// </returns>
    public ProviderRegistryRegistrationResult Register(ProviderManifestDescriptor? descriptor)
    {
        var validation = _validator.Validate(descriptor);
        if (!validation.Succeeded)
        {
            return new ProviderRegistryRegistrationResult
            {
                Succeeded = false,
                ErrorCode = validation.ErrorCode,
                ErrorMessage = validation.ErrorMessage,
                Diagnostics = validation.Diagnostics
            };
        }

        _providers.Add(descriptor!);
        _providers.Sort(CompareProviders);

        return new ProviderRegistryRegistrationResult
        {
            Succeeded = true,
            Provider = descriptor,
            Diagnostics =
            [
                new ProviderDiagnostic
                {
                    Code = "PROVIDER_REGISTERED",
                    Message = "Provider manifest registered.",
                    Severity = "Information",
                    Source = descriptor!.Source
                }
            ]
        };
    }

    /// <summary>
    /// [EN] Lists registered providers in deterministic order.
    /// [JA] 登録済み Provider を deterministic order で一覧します。
    /// </summary>
    /// <returns>EN:  JA: 結果を返します。
    /// [EN] Registered provider descriptors.
    /// [JA] 登録済み Provider descriptor です。
    /// </returns>
    public IReadOnlyList<ProviderManifestDescriptor> List()
        => _providers.ToArray();

    private static int CompareProviders(
        ProviderManifestDescriptor left,
        ProviderManifestDescriptor right)
    {
        var byId = string.Compare(left.ProviderId, right.ProviderId, StringComparison.Ordinal);
        if (byId != 0)
        {
            return byId;
        }

        var byVersion = string.Compare(left.Version, right.Version, StringComparison.Ordinal);
        if (byVersion != 0)
        {
            return byVersion;
        }

        return string.Compare(left.AssemblyName, right.AssemblyName, StringComparison.Ordinal);
    }
}
