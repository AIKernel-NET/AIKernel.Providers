namespace AIKernel.Providers.MicrosoftAI;

using AIKernel.Abstractions.Models;
using AIKernel.Abstractions.Providers;
using AIKernel.Dtos.Core;
using AIKernel.Dtos.Routing;

/// <include file="docs.en.xml" path="doc/members/member[@name='T:AIKernel.Providers.MicrosoftAI.OpenAICompatibleProviderCapabilities']/summary" />
/// <include file="docs.ja.xml" path="doc/members/member[@name='T:AIKernel.Providers.MicrosoftAI.OpenAICompatibleProviderCapabilities']/summary" />
public sealed class OpenAICompatibleProviderCapabilities : IProviderCapabilities
{
    private static readonly string[] Operations =
    [
        "chat",
        "text-generation"
    ];

    private static readonly string[] DataTypes =
    [
        "text"
    ];

    /// <include file="docs.en.xml" path="doc/members/member[@name='F:AIKernel.Providers.MicrosoftAI.OpenAICompatibleProviderCapabilities.SupportedOperations']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='F:AIKernel.Providers.MicrosoftAI.OpenAICompatibleProviderCapabilities.SupportedOperations']/summary" />
    public IReadOnlyList<string> SupportedOperations => Operations;

    /// <include file="docs.en.xml" path="doc/members/member[@name='F:AIKernel.Providers.MicrosoftAI.OpenAICompatibleProviderCapabilities.SupportedDataTypes']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='F:AIKernel.Providers.MicrosoftAI.OpenAICompatibleProviderCapabilities.SupportedDataTypes']/summary" />
    public IReadOnlyList<string> SupportedDataTypes => DataTypes;

    /// <include file="docs.en.xml" path="doc/members/member[@name='F:AIKernel.Providers.MicrosoftAI.OpenAICompatibleProviderCapabilities.MaxConcurrentConnections']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='F:AIKernel.Providers.MicrosoftAI.OpenAICompatibleProviderCapabilities.MaxConcurrentConnections']/summary" />
    public int MaxConcurrentConnections => 1;

    /// <include file="docs.en.xml" path="doc/members/member[@name='F:AIKernel.Providers.MicrosoftAI.OpenAICompatibleProviderCapabilities.RateLimit']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='F:AIKernel.Providers.MicrosoftAI.OpenAICompatibleProviderCapabilities.RateLimit']/summary" />
    public RateLimitInfo? RateLimit => null;

    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Providers.MicrosoftAI.OpenAICompatibleProviderCapabilities.new']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Providers.MicrosoftAI.OpenAICompatibleProviderCapabilities.new']/summary" />
    public ModelCapacityVector Vector => new();

    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Providers.MicrosoftAI.OpenAICompatibleProviderCapabilities.GetDynamicCapacities']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Providers.MicrosoftAI.OpenAICompatibleProviderCapabilities.GetDynamicCapacities']/summary" />
    public IDictionary<string, float>? GetDynamicCapacities(
        IExecutionConstraints constraints)
    {
        return null;
    }

    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Providers.MicrosoftAI.OpenAICompatibleProviderCapabilities.GetCapabilityProfile']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Providers.MicrosoftAI.OpenAICompatibleProviderCapabilities.GetCapabilityProfile']/summary" />
    public ICapabilityProfile? GetCapabilityProfile()
    {
        return null;
    }

    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Providers.MicrosoftAI.OpenAICompatibleProviderCapabilities.SupportsOperation']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Providers.MicrosoftAI.OpenAICompatibleProviderCapabilities.SupportsOperation']/summary" />
    public bool SupportsOperation(
        string operation)
    {
        return Operations.Contains(operation, StringComparer.OrdinalIgnoreCase);
    }

    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Providers.MicrosoftAI.OpenAICompatibleProviderCapabilities.SupportsDataType']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Providers.MicrosoftAI.OpenAICompatibleProviderCapabilities.SupportsDataType']/summary" />
    public bool SupportsDataType(
        string dataType)
    {
        return DataTypes.Contains(dataType, StringComparer.OrdinalIgnoreCase);
    }

    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Providers.MicrosoftAI.OpenAICompatibleProviderCapabilities.SupportsQuantization']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Providers.MicrosoftAI.OpenAICompatibleProviderCapabilities.SupportsQuantization']/summary" />
    public bool SupportsQuantization(
        string quantizationLevel)
    {
        return false;
    }

    /// <include file="docs.en.xml" path="doc/members/member[@name='F:AIKernel.Providers.MicrosoftAI.OpenAICompatibleProviderCapabilities.SupportsQueryAugmentation']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='F:AIKernel.Providers.MicrosoftAI.OpenAICompatibleProviderCapabilities.SupportsQueryAugmentation']/summary" />
    public bool SupportsQueryAugmentation => false;

    /// <include file="docs.en.xml" path="doc/members/member[@name='F:AIKernel.Providers.MicrosoftAI.OpenAICompatibleProviderCapabilities.SupportsQueryDecomposition']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='F:AIKernel.Providers.MicrosoftAI.OpenAICompatibleProviderCapabilities.SupportsQueryDecomposition']/summary" />
    public bool SupportsQueryDecomposition => false;

    /// <include file="docs.en.xml" path="doc/members/member[@name='F:AIKernel.Providers.MicrosoftAI.OpenAICompatibleProviderCapabilities.SupportsQueryRouting']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='F:AIKernel.Providers.MicrosoftAI.OpenAICompatibleProviderCapabilities.SupportsQueryRouting']/summary" />
    public bool SupportsQueryRouting => false;

    /// <include file="docs.en.xml" path="doc/members/member[@name='F:AIKernel.Providers.MicrosoftAI.OpenAICompatibleProviderCapabilities.MaxQueryParts']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='F:AIKernel.Providers.MicrosoftAI.OpenAICompatibleProviderCapabilities.MaxQueryParts']/summary" />
    public int MaxQueryParts => 0;

    /// <include file="docs.en.xml" path="doc/members/member[@name='F:AIKernel.Providers.MicrosoftAI.OpenAICompatibleProviderCapabilities.SupportedQueryProcessingOperations']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='F:AIKernel.Providers.MicrosoftAI.OpenAICompatibleProviderCapabilities.SupportedQueryProcessingOperations']/summary" />
    public IReadOnlyList<string> SupportedQueryProcessingOperations => [];

    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Providers.MicrosoftAI.OpenAICompatibleProviderCapabilities.SupportsQueryProcessingOperation']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Providers.MicrosoftAI.OpenAICompatibleProviderCapabilities.SupportsQueryProcessingOperation']/summary" />
    public bool SupportsQueryProcessingOperation(
        string operation)
    {
        return false;
    }

    /// <include file="docs.en.xml" path="doc/members/member[@name='F:AIKernel.Providers.MicrosoftAI.OpenAICompatibleProviderCapabilities.SupportsEmbedding']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='F:AIKernel.Providers.MicrosoftAI.OpenAICompatibleProviderCapabilities.SupportsEmbedding']/summary" />
    public bool SupportsEmbedding => false;

    /// <include file="docs.en.xml" path="doc/members/member[@name='F:AIKernel.Providers.MicrosoftAI.OpenAICompatibleProviderCapabilities.EmbeddingDimensions']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='F:AIKernel.Providers.MicrosoftAI.OpenAICompatibleProviderCapabilities.EmbeddingDimensions']/summary" />
    public int? EmbeddingDimensions => null;

    /// <include file="docs.en.xml" path="doc/members/member[@name='F:AIKernel.Providers.MicrosoftAI.OpenAICompatibleProviderCapabilities.SupportedEmbeddingModels']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='F:AIKernel.Providers.MicrosoftAI.OpenAICompatibleProviderCapabilities.SupportedEmbeddingModels']/summary" />
    public IReadOnlyList<string> SupportedEmbeddingModels => [];
}
