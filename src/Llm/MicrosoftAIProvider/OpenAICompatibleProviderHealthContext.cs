namespace AIKernel.Providers.MicrosoftAI;

/// <include file="docs.en.xml" path="doc/members/member[@name='T:AIKernel.Providers.MicrosoftAI.OpenAICompatibleProviderHealthContext']/summary" />
/// <include file="docs.ja.xml" path="doc/members/member[@name='T:AIKernel.Providers.MicrosoftAI.OpenAICompatibleProviderHealthContext']/summary" />
public sealed record OpenAICompatibleProviderHealthContext
{
    /// <include file="docs.en.xml" path="doc/members/member[@name='P:AIKernel.Providers.MicrosoftAI.OpenAICompatibleProviderHealthContext.ProviderId']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='P:AIKernel.Providers.MicrosoftAI.OpenAICompatibleProviderHealthContext.ProviderId']/summary" />
    public required string ProviderId { get; init; }

    /// <include file="docs.en.xml" path="doc/members/member[@name='P:AIKernel.Providers.MicrosoftAI.OpenAICompatibleProviderHealthContext.Name']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='P:AIKernel.Providers.MicrosoftAI.OpenAICompatibleProviderHealthContext.Name']/summary" />
    public required string Name { get; init; }

    /// <include file="docs.en.xml" path="doc/members/member[@name='P:AIKernel.Providers.MicrosoftAI.OpenAICompatibleProviderHealthContext.Version']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='P:AIKernel.Providers.MicrosoftAI.OpenAICompatibleProviderHealthContext.Version']/summary" />
    public required string Version { get; init; }

    /// <include file="docs.en.xml" path="doc/members/member[@name='P:AIKernel.Providers.MicrosoftAI.OpenAICompatibleProviderHealthContext.ModelId']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='P:AIKernel.Providers.MicrosoftAI.OpenAICompatibleProviderHealthContext.ModelId']/summary" />
    public required string ModelId { get; init; }

    /// <include file="docs.en.xml" path="doc/members/member[@name='P:AIKernel.Providers.MicrosoftAI.OpenAICompatibleProviderHealthContext.IsInitialized']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='P:AIKernel.Providers.MicrosoftAI.OpenAICompatibleProviderHealthContext.IsInitialized']/summary" />
    public required bool IsInitialized { get; init; }

    /// <include file="docs.en.xml" path="doc/members/member[@name='P:AIKernel.Providers.MicrosoftAI.OpenAICompatibleProviderHealthContext.CheckedAtUtc']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='P:AIKernel.Providers.MicrosoftAI.OpenAICompatibleProviderHealthContext.CheckedAtUtc']/summary" />
    public required DateTimeOffset CheckedAtUtc { get; init; }
}