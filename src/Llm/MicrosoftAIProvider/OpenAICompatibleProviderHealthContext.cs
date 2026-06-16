namespace AIKernel.Providers.MicrosoftAI;

/// <summary>[EN] Documents this public package API member. [JA] OpenAICompatibleProviderHealthContext を表します。</summary>
/// <include file="docs.en.xml" path="doc/members/member[@name='T:AIKernel.Providers.MicrosoftAI.OpenAICompatibleProviderHealthContext']/summary" />
/// <include file="docs.ja.xml" path="doc/members/member[@name='T:AIKernel.Providers.MicrosoftAI.OpenAICompatibleProviderHealthContext']/summary" />
public sealed record OpenAICompatibleProviderHealthContext
{
    /// <summary>[EN] Documents this public package API member. [JA] ProviderId を取得します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='P:AIKernel.Providers.MicrosoftAI.OpenAICompatibleProviderHealthContext.ProviderId']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='P:AIKernel.Providers.MicrosoftAI.OpenAICompatibleProviderHealthContext.ProviderId']/summary" />
    public required string ProviderId { get; init; }

    /// <summary>[EN] Documents this public package API member. [JA] Name を取得します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='P:AIKernel.Providers.MicrosoftAI.OpenAICompatibleProviderHealthContext.Name']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='P:AIKernel.Providers.MicrosoftAI.OpenAICompatibleProviderHealthContext.Name']/summary" />
    public required string Name { get; init; }

    /// <summary>[EN] Documents this public package API member. [JA] Version を取得します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='P:AIKernel.Providers.MicrosoftAI.OpenAICompatibleProviderHealthContext.Version']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='P:AIKernel.Providers.MicrosoftAI.OpenAICompatibleProviderHealthContext.Version']/summary" />
    public required string Version { get; init; }

    /// <summary>[EN] Documents this public package API member. [JA] ModelId を取得します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='P:AIKernel.Providers.MicrosoftAI.OpenAICompatibleProviderHealthContext.ModelId']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='P:AIKernel.Providers.MicrosoftAI.OpenAICompatibleProviderHealthContext.ModelId']/summary" />
    public required string ModelId { get; init; }

    /// <summary>[EN] Documents this public package API member. [JA] IsInitialized を取得します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='P:AIKernel.Providers.MicrosoftAI.OpenAICompatibleProviderHealthContext.IsInitialized']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='P:AIKernel.Providers.MicrosoftAI.OpenAICompatibleProviderHealthContext.IsInitialized']/summary" />
    public required bool IsInitialized { get; init; }

    /// <summary>[EN] Documents this public package API member. [JA] CheckedAtUtc を取得します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='P:AIKernel.Providers.MicrosoftAI.OpenAICompatibleProviderHealthContext.CheckedAtUtc']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='P:AIKernel.Providers.MicrosoftAI.OpenAICompatibleProviderHealthContext.CheckedAtUtc']/summary" />
    public required DateTimeOffset CheckedAtUtc { get; init; }
}