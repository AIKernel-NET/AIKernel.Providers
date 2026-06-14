namespace AIKernel.Providers.Substrate;

/// <summary>
/// [EN] Deterministic merge and override rules for loose provider manifest extensions.
/// [JA] loose Provider manifest extension 向けの deterministic merge / override rule です。
/// </summary>
public sealed class ProviderManifestMergePolicy
{
    /// <summary>
    /// [EN] Merges provider-level, backend-level, and vendor-level metadata deterministically.
    /// [JA] provider-level、backend-level、vendor-level metadata を deterministic に merge します。
    /// </summary>
    /// <param name="descriptor">
    /// [EN] Provider manifest descriptor.
    /// [JA] Provider manifest descriptor です。
    /// </param>
    /// <returns>
    /// [EN] Merged metadata map.
    /// [JA] merge 済み metadata map です。
    /// </returns>
    public IReadOnlyDictionary<string, string> MergeMetadata(ProviderManifestDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);

        var merged = new SortedDictionary<string, string>(StringComparer.Ordinal);
        CopyInto(merged, descriptor.Metadata);
        CopyInto(merged, descriptor.BackendMetadata);
        CopyInto(merged, descriptor.VendorMetadata);
        return merged;
    }

    /// <summary>
    /// [EN] Merges CLI hints by inheriting provider hints and allowing override hints to replace scalar fields and append lists.
    /// [JA] provider hint を継承し、override hint が scalar field を置換し list を追加できるように CLI hint を merge します。
    /// </summary>
    /// <param name="providerHints">
    /// [EN] Provider-level CLI hints.
    /// [JA] provider-level CLI hint です。
    /// </param>
    /// <param name="overrideHints">
    /// [EN] Override CLI hints.
    /// [JA] override CLI hint です。
    /// </param>
    /// <returns>
    /// [EN] Merged CLI hints.
    /// [JA] merge 済み CLI hint です。
    /// </returns>
    public ProviderCliHints MergeCliHints(
        ProviderCliHints providerHints,
        ProviderCliHints? overrideHints)
    {
        ArgumentNullException.ThrowIfNull(providerHints);

        if (overrideHints is null)
        {
            return providerHints;
        }

        return new ProviderCliHints
        {
            Command = FirstNonEmpty(overrideHints.Command, providerHints.Command),
            DefaultOperation = FirstNonEmpty(overrideHints.DefaultOperation, providerHints.DefaultOperation),
            Commands = MergeLists(providerHints.Commands, overrideHints.Commands),
            ConfigKeys = MergeLists(providerHints.ConfigKeys, overrideHints.ConfigKeys),
            RequiredEnvironment = MergeLists(providerHints.RequiredEnvironment, overrideHints.RequiredEnvironment),
            ExtensionJson = MergeMaps(providerHints.ExtensionJson, overrideHints.ExtensionJson)
        };
    }

    private static void CopyInto(
        SortedDictionary<string, string> target,
        IReadOnlyDictionary<string, string> values)
    {
        foreach (var item in values.OrderBy(item => item.Key, StringComparer.Ordinal))
        {
            target[item.Key] = item.Value;
        }
    }

    private static IReadOnlyDictionary<string, string> MergeMaps(
        IReadOnlyDictionary<string, string> providerValues,
        IReadOnlyDictionary<string, string> overrideValues)
    {
        var merged = new SortedDictionary<string, string>(StringComparer.Ordinal);
        CopyInto(merged, providerValues);
        CopyInto(merged, overrideValues);
        return merged;
    }

    private static IReadOnlyList<string> MergeLists(
        IReadOnlyList<string> providerValues,
        IReadOnlyList<string> overrideValues)
        => providerValues
            .Concat(overrideValues)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();

    private static string? FirstNonEmpty(string? first, string? second)
        => !string.IsNullOrWhiteSpace(first) ? first : second;
}
