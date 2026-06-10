namespace AIKernel.Providers.MicrosoftAI;

using System.Collections.Immutable;

/// <include file="docs.en.xml" path="doc/members/member[@name='T:AIKernel.Providers.MicrosoftAI.OpenAICompatibleResponseProjection']/summary" />
/// <include file="docs.ja.xml" path="doc/members/member[@name='T:AIKernel.Providers.MicrosoftAI.OpenAICompatibleResponseProjection']/summary" />
public sealed record OpenAICompatibleResponseProjection
{
    /// <include file="docs.en.xml" path="doc/members/member[@name='P:AIKernel.Providers.MicrosoftAI.OpenAICompatibleResponseProjection.ModelId']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='P:AIKernel.Providers.MicrosoftAI.OpenAICompatibleResponseProjection.ModelId']/summary" />
    public required string ModelId { get; init; }

    /// <include file="docs.en.xml" path="doc/members/member[@name='P:AIKernel.Providers.MicrosoftAI.OpenAICompatibleResponseProjection.RawResponse']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='P:AIKernel.Providers.MicrosoftAI.OpenAICompatibleResponseProjection.RawResponse']/summary" />
    public required string RawResponse { get; init; }

    /// <include file="docs.en.xml" path="doc/members/member[@name='P:AIKernel.Providers.MicrosoftAI.OpenAICompatibleResponseProjection.PrimaryText']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='P:AIKernel.Providers.MicrosoftAI.OpenAICompatibleResponseProjection.PrimaryText']/summary" />
    public required string PrimaryText { get; init; }

    /// <include file="docs.en.xml" path="doc/members/member[@name='P:AIKernel.Providers.MicrosoftAI.OpenAICompatibleResponseProjection.IsTruncated']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='P:AIKernel.Providers.MicrosoftAI.OpenAICompatibleResponseProjection.IsTruncated']/summary" />
    public required bool IsTruncated { get; init; }

    /// <include file="docs.en.xml" path="doc/members/member[@name='P:AIKernel.Providers.MicrosoftAI.OpenAICompatibleResponseProjection.ObservedAtUtc']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='P:AIKernel.Providers.MicrosoftAI.OpenAICompatibleResponseProjection.ObservedAtUtc']/summary" />
    public required DateTimeOffset ObservedAtUtc { get; init; }

    /// <include file="docs.en.xml" path="doc/members/member[@name='P:AIKernel.Providers.MicrosoftAI.OpenAICompatibleResponseProjection.string']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='P:AIKernel.Providers.MicrosoftAI.OpenAICompatibleResponseProjection.string']/summary" />
    public required ImmutableDictionary<string, string> Metadata { get; init; }
}
