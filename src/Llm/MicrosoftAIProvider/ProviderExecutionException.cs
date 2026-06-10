namespace AIKernel.Providers.MicrosoftAI;

/// <include file="docs.en.xml" path="doc/members/member[@name='T:AIKernel.Providers.MicrosoftAI.ProviderExecutionException']/summary" />
/// <include file="docs.ja.xml" path="doc/members/member[@name='T:AIKernel.Providers.MicrosoftAI.ProviderExecutionException']/summary" />
public abstract class ProviderExecutionException : Exception
{
    /// <summary>[EN] Initializes a new instance for the ProviderExecutionException AIKernel contract surface. [JA] ProviderExecutionException AIKernel 契約サーフェスの新しいインスタンスを初期化します。</summary>
    protected ProviderExecutionException(string message)
        : base(message)
    {
    }

    /// <summary>[EN] Initializes a new instance for the ProviderExecutionException AIKernel contract surface. [JA] ProviderExecutionException AIKernel 契約サーフェスの新しいインスタンスを初期化します。</summary>
    protected ProviderExecutionException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    /// <include file="docs.en.xml" path="doc/members/member[@name='P:AIKernel.Providers.MicrosoftAI.ProviderExecutionException.string']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='P:AIKernel.Providers.MicrosoftAI.ProviderExecutionException.string']/summary" />
    public abstract string ErrorCode { get; }
}

/// <include file="docs.en.xml" path="doc/members/member[@name='T:AIKernel.Providers.MicrosoftAI.ProviderInvalidResponseException']/summary" />
/// <include file="docs.ja.xml" path="doc/members/member[@name='T:AIKernel.Providers.MicrosoftAI.ProviderInvalidResponseException']/summary" />
public sealed class ProviderInvalidResponseException : ProviderExecutionException
{
    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Providers.MicrosoftAI.ProviderInvalidResponseException.#ctor']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Providers.MicrosoftAI.ProviderInvalidResponseException.#ctor']/summary" />
    public ProviderInvalidResponseException(string message)
        : base(message)
    {
    }

    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Providers.MicrosoftAI.ProviderInvalidResponseException.#ctor']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Providers.MicrosoftAI.ProviderInvalidResponseException.#ctor']/summary" />
    public ProviderInvalidResponseException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    /// <include file="docs.en.xml" path="doc/members/member[@name='F:AIKernel.Providers.MicrosoftAI.ProviderInvalidResponseException.ErrorCode']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='F:AIKernel.Providers.MicrosoftAI.ProviderInvalidResponseException.ErrorCode']/summary" />
    public override string ErrorCode => "invalid_provider_response";
}

/// <include file="docs.en.xml" path="doc/members/member[@name='T:AIKernel.Providers.MicrosoftAI.ProviderCapabilityMismatchException']/summary" />
/// <include file="docs.ja.xml" path="doc/members/member[@name='T:AIKernel.Providers.MicrosoftAI.ProviderCapabilityMismatchException']/summary" />
public sealed class ProviderCapabilityMismatchException(string message) : ProviderExecutionException(message)
{
    /// <include file="docs.en.xml" path="doc/members/member[@name='F:AIKernel.Providers.MicrosoftAI.ProviderCapabilityMismatchException.ErrorCode']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='F:AIKernel.Providers.MicrosoftAI.ProviderCapabilityMismatchException.ErrorCode']/summary" />
    public override string ErrorCode => "capability_mismatch";
}

/// <include file="docs.en.xml" path="doc/members/member[@name='T:AIKernel.Providers.MicrosoftAI.ProviderExecutionTimeoutException']/summary" />
/// <include file="docs.ja.xml" path="doc/members/member[@name='T:AIKernel.Providers.MicrosoftAI.ProviderExecutionTimeoutException']/summary" />
public sealed class ProviderExecutionTimeoutException(string message, Exception innerException) : ProviderExecutionException(message, innerException)
{
    /// <include file="docs.en.xml" path="doc/members/member[@name='F:AIKernel.Providers.MicrosoftAI.ProviderExecutionTimeoutException.ErrorCode']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='F:AIKernel.Providers.MicrosoftAI.ProviderExecutionTimeoutException.ErrorCode']/summary" />
    public override string ErrorCode => "provider_timeout";
}

/// <include file="docs.en.xml" path="doc/members/member[@name='T:AIKernel.Providers.MicrosoftAI.ProviderRateLimitException']/summary" />
/// <include file="docs.ja.xml" path="doc/members/member[@name='T:AIKernel.Providers.MicrosoftAI.ProviderRateLimitException']/summary" />
public sealed class ProviderRateLimitException(string message, Exception innerException) : ProviderExecutionException(message, innerException)
{
    /// <include file="docs.en.xml" path="doc/members/member[@name='F:AIKernel.Providers.MicrosoftAI.ProviderRateLimitException.ErrorCode']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='F:AIKernel.Providers.MicrosoftAI.ProviderRateLimitException.ErrorCode']/summary" />
    public override string ErrorCode => "rate_limited";
}

/// <include file="docs.en.xml" path="doc/members/member[@name='T:AIKernel.Providers.MicrosoftAI.ProviderApiException']/summary" />
/// <include file="docs.ja.xml" path="doc/members/member[@name='T:AIKernel.Providers.MicrosoftAI.ProviderApiException']/summary" />
public sealed class ProviderApiException(string message, Exception innerException) : ProviderExecutionException(message, innerException)
{
    /// <include file="docs.en.xml" path="doc/members/member[@name='F:AIKernel.Providers.MicrosoftAI.ProviderApiException.ErrorCode']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='F:AIKernel.Providers.MicrosoftAI.ProviderApiException.ErrorCode']/summary" />
    public override string ErrorCode => "provider_api_error";
}
