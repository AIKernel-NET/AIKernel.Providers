namespace AIKernel.Providers.MicrosoftAI;

using AIKernel.Abstractions.Providers;
using AIKernel.Abstractions.Security;
using AIKernel.Dtos.Core;

/// <summary>[EN] Documents this public package API member. [JA] ISecureOptions を表します。</summary>
/// <include file="docs.en.xml" path="doc/members/member[@name='T:AIKernel.Providers.MicrosoftAI.OpenAICompatibleProviderOptions']/summary" />
/// <include file="docs.ja.xml" path="doc/members/member[@name='T:AIKernel.Providers.MicrosoftAI.OpenAICompatibleProviderOptions']/summary" />
public sealed record OpenAICompatibleProviderOptions : ISecureOptions
{
    /// <summary>[EN] Documents this public package API member. [JA] ProviderId を取得します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='P:AIKernel.Providers.MicrosoftAI.OpenAICompatibleProviderOptions.ProviderId']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='P:AIKernel.Providers.MicrosoftAI.OpenAICompatibleProviderOptions.ProviderId']/summary" />
    public string ProviderId { get; init; } = "openai-compatible";

    /// <summary>[EN] Documents this public package API member. [JA] Name を取得します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='P:AIKernel.Providers.MicrosoftAI.OpenAICompatibleProviderOptions.Name']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='P:AIKernel.Providers.MicrosoftAI.OpenAICompatibleProviderOptions.Name']/summary" />
    public string Name { get; init; } = "OpenAI Compatible Provider";

    /// <summary>[EN] Documents this public package API member. [JA] Version を取得します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='P:AIKernel.Providers.MicrosoftAI.OpenAICompatibleProviderOptions.Version']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='P:AIKernel.Providers.MicrosoftAI.OpenAICompatibleProviderOptions.Version']/summary" />
    public string Version { get; init; } = "0.1.1";

    /// <summary>[EN] Documents this public package API member. [JA] ModelId を取得します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='P:AIKernel.Providers.MicrosoftAI.OpenAICompatibleProviderOptions.ModelId']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='P:AIKernel.Providers.MicrosoftAI.OpenAICompatibleProviderOptions.ModelId']/summary" />
    public string ModelId { get; init; } = "";

    /// <summary>[EN] Documents this public package API member. [JA] Endpoint を取得します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='P:AIKernel.Providers.MicrosoftAI.OpenAICompatibleProviderOptions.Endpoint']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='P:AIKernel.Providers.MicrosoftAI.OpenAICompatibleProviderOptions.Endpoint']/summary" />
    public Uri? Endpoint { get; init; }

    /// <summary>[EN] Documents this public package API member. [JA] MaxInputTokens を取得します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='P:AIKernel.Providers.MicrosoftAI.OpenAICompatibleProviderOptions.MaxInputTokens']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='P:AIKernel.Providers.MicrosoftAI.OpenAICompatibleProviderOptions.MaxInputTokens']/summary" />
    public int MaxInputTokens { get; init; } = 8192;

    /// <summary>[EN] Documents this public package API member. [JA] MaxOutputTokens を取得します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='P:AIKernel.Providers.MicrosoftAI.OpenAICompatibleProviderOptions.MaxOutputTokens']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='P:AIKernel.Providers.MicrosoftAI.OpenAICompatibleProviderOptions.MaxOutputTokens']/summary" />
    public int? MaxOutputTokens { get; init; }

    /// <summary>[EN] Documents this public package API member. [JA] Temperature を取得します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='P:AIKernel.Providers.MicrosoftAI.OpenAICompatibleProviderOptions.Temperature']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='P:AIKernel.Providers.MicrosoftAI.OpenAICompatibleProviderOptions.Temperature']/summary" />
    public float? Temperature { get; init; }

    /// <summary>[EN] Documents this public package API member. [JA] TopP を取得します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='P:AIKernel.Providers.MicrosoftAI.OpenAICompatibleProviderOptions.TopP']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='P:AIKernel.Providers.MicrosoftAI.OpenAICompatibleProviderOptions.TopP']/summary" />
    public float? TopP { get; init; }

    /// <summary>[EN] Documents this public package API member. [JA] SupportsSystemRole を取得します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='P:AIKernel.Providers.MicrosoftAI.OpenAICompatibleProviderOptions.SupportsSystemRole']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='P:AIKernel.Providers.MicrosoftAI.OpenAICompatibleProviderOptions.SupportsSystemRole']/summary" />
    public bool SupportsSystemRole { get; init; } = true;

    /// <summary>[EN] Documents this public package API member. [JA] SupportsAssistantRole を取得します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='P:AIKernel.Providers.MicrosoftAI.OpenAICompatibleProviderOptions.SupportsAssistantRole']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='P:AIKernel.Providers.MicrosoftAI.OpenAICompatibleProviderOptions.SupportsAssistantRole']/summary" />
    public bool SupportsAssistantRole { get; init; } = true;

    /// <summary>[EN] Documents this public package API member. [JA] SupportsToolRole を取得します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='P:AIKernel.Providers.MicrosoftAI.OpenAICompatibleProviderOptions.SupportsToolRole']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='P:AIKernel.Providers.MicrosoftAI.OpenAICompatibleProviderOptions.SupportsToolRole']/summary" />
    public bool SupportsToolRole { get; init; }

    /// <summary>[EN] Documents this public package API member. [JA] SupportsStreaming を取得します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='P:AIKernel.Providers.MicrosoftAI.OpenAICompatibleProviderOptions.SupportsStreaming']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='P:AIKernel.Providers.MicrosoftAI.OpenAICompatibleProviderOptions.SupportsStreaming']/summary" />
    public bool SupportsStreaming { get; init; }

    /// <summary>[EN] Documents this public package API member. [JA] StopSequences を取得します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='P:AIKernel.Providers.MicrosoftAI.OpenAICompatibleProviderOptions.StopSequences']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='P:AIKernel.Providers.MicrosoftAI.OpenAICompatibleProviderOptions.StopSequences']/summary" />
    public IReadOnlyList<string> StopSequences { get; init; } = [];

    /// <summary>[EN] Documents this public package API member. [JA] SecretKeyName を取得します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='P:AIKernel.Providers.MicrosoftAI.OpenAICompatibleProviderOptions.SecretKeyName']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='P:AIKernel.Providers.MicrosoftAI.OpenAICompatibleProviderOptions.SecretKeyName']/summary" />
    public string? SecretKeyName { get; set; }

    /// <summary>[EN] Documents this public package API member. [JA] ApiKey を取得します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='P:AIKernel.Providers.MicrosoftAI.OpenAICompatibleProviderOptions.ApiKey']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='P:AIKernel.Providers.MicrosoftAI.OpenAICompatibleProviderOptions.ApiKey']/summary" />
    public string? ApiKey { get; set; }

    /// <summary>
    /// [EN] Gets a factory that creates explicit provider health status values without relying on default instances.
    /// [JA] default インスタンスに依存せず、明示的な ProviderHealthStatus 値を生成する factory を取得します。
    /// </summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='P:AIKernel.Providers.MicrosoftAI.OpenAICompatibleProviderOptions.ProviderHealthStatus']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='P:AIKernel.Providers.MicrosoftAI.OpenAICompatibleProviderOptions.ProviderHealthStatus']/summary" />
    public Func<OpenAICompatibleProviderHealthContext, ProviderHealthStatus>? HealthStatusFactory { get; init; }

    /// <summary>[EN] Documents this public package API member. [JA] ToString を実行します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Providers.MicrosoftAI.OpenAICompatibleProviderOptions.ToString']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Providers.MicrosoftAI.OpenAICompatibleProviderOptions.ToString']/summary" />
    public override string ToString()
    {
        return
            $"OpenAICompatibleProviderOptions {{ ProviderId = {ProviderId}, Name = {Name}, Version = {Version}, ModelId = {ModelId}, SecretKeyName = {SecretKeyName}, ApiKey = ***REDACTED*** }}";
    }
}
