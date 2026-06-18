namespace AIKernel.Providers.MicrosoftAI;

using System.Collections.Immutable;

/// <summary>[EN] Documents this public package API member. [JA] OpenAICompatibleResponseProjection を表します。</summary>
/// <include file="docs.en.xml" path="doc/members/member[@name='T:AIKernel.Providers.MicrosoftAI.OpenAICompatibleResponseProjection']/summary" />
/// <include file="docs.ja.xml" path="doc/members/member[@name='T:AIKernel.Providers.MicrosoftAI.OpenAICompatibleResponseProjection']/summary" />
public sealed record OpenAICompatibleResponseProjection
{
    /// <summary>[EN] Documents this public package API member. [JA] ModelId を取得します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='P:AIKernel.Providers.MicrosoftAI.OpenAICompatibleResponseProjection.ModelId']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='P:AIKernel.Providers.MicrosoftAI.OpenAICompatibleResponseProjection.ModelId']/summary" />
    public required string ModelId { get; init; }

    /// <summary>[EN] Documents this public package API member. [JA] RawResponse を取得します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='P:AIKernel.Providers.MicrosoftAI.OpenAICompatibleResponseProjection.RawResponse']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='P:AIKernel.Providers.MicrosoftAI.OpenAICompatibleResponseProjection.RawResponse']/summary" />
    public required string RawResponse { get; init; }

    /// <summary>[EN] Documents this public package API member. [JA] PrimaryText を取得します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='P:AIKernel.Providers.MicrosoftAI.OpenAICompatibleResponseProjection.PrimaryText']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='P:AIKernel.Providers.MicrosoftAI.OpenAICompatibleResponseProjection.PrimaryText']/summary" />
    public required string PrimaryText { get; init; }

    /// <summary>[EN] Documents this public package API member. [JA] IsTruncated を取得します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='P:AIKernel.Providers.MicrosoftAI.OpenAICompatibleResponseProjection.IsTruncated']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='P:AIKernel.Providers.MicrosoftAI.OpenAICompatibleResponseProjection.IsTruncated']/summary" />
    public required bool IsTruncated { get; init; }

    /// <summary>[EN] Documents this public package API member. [JA] ObservedAtUtc を取得します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='P:AIKernel.Providers.MicrosoftAI.OpenAICompatibleResponseProjection.ObservedAtUtc']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='P:AIKernel.Providers.MicrosoftAI.OpenAICompatibleResponseProjection.ObservedAtUtc']/summary" />
    public required DateTimeOffset ObservedAtUtc { get; init; }

    /// <summary>[EN] Documents this public package API member. [JA] Metadata を取得します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='P:AIKernel.Providers.MicrosoftAI.OpenAICompatibleResponseProjection.string']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='P:AIKernel.Providers.MicrosoftAI.OpenAICompatibleResponseProjection.string']/summary" />
    public required ImmutableDictionary<string, string> Metadata { get; init; }
}
