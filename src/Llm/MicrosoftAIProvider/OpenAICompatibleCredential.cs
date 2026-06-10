namespace AIKernel.Providers.MicrosoftAI;

using AIKernel.Core.Security;
using AIKernel.Core.Time;

/// <include file="docs.en.xml" path="doc/members/member[@name='T:AIKernel.Providers.MicrosoftAI.OpenAICompatibleCredential']/summary" />
/// <include file="docs.ja.xml" path="doc/members/member[@name='T:AIKernel.Providers.MicrosoftAI.OpenAICompatibleCredential']/summary" />
public sealed record OpenAICompatibleCredential
{
    /// <include file="docs.en.xml" path="doc/members/member[@name='P:AIKernel.Providers.MicrosoftAI.OpenAICompatibleCredential.ApiKey']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='P:AIKernel.Providers.MicrosoftAI.OpenAICompatibleCredential.ApiKey']/summary" />
    public required string ApiKey { get; init; }

    /// <include file="docs.en.xml" path="doc/members/member[@name='P:AIKernel.Providers.MicrosoftAI.OpenAICompatibleCredential.ExpiresAtUtc']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='P:AIKernel.Providers.MicrosoftAI.OpenAICompatibleCredential.ExpiresAtUtc']/summary" />
    public DateTimeOffset? ExpiresAtUtc { get; init; }

    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Providers.MicrosoftAI.OpenAICompatibleCredential.Create']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Providers.MicrosoftAI.OpenAICompatibleCredential.Create']/summary" />
    public static OpenAICompatibleCredential Create(
        string keyName,
        string apiKey,
        DateTimeOffset? expiresAtUtc,
        IKernelClock? clock = null)
    {
        SecureCredentialGuard.ValidateSecret(
            keyName,
            apiKey,
            expiresAtUtc,
            clock?.Logical);

        return new OpenAICompatibleCredential
        {
            ApiKey = apiKey,
            ExpiresAtUtc = expiresAtUtc
        };
    }

    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Providers.MicrosoftAI.OpenAICompatibleCredential.ToString']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Providers.MicrosoftAI.OpenAICompatibleCredential.ToString']/summary" />
    public override string ToString()
    {
        return "OpenAICompatibleCredential { ApiKey = ***REDACTED*** }";
    }
}
