namespace AIKernel.Providers.MicrosoftAI;

using Microsoft.Extensions.AI;

/// <summary>[EN] Documents this public package API member. [JA] IOpenAICompatibleResponseMapper contract を定義します。</summary>
/// <include file="docs.en.xml" path="doc/members/member[@name='T:AIKernel.Providers.MicrosoftAI.IOpenAICompatibleResponseMapper']/summary" />
/// <include file="docs.ja.xml" path="doc/members/member[@name='T:AIKernel.Providers.MicrosoftAI.IOpenAICompatibleResponseMapper']/summary" />
public interface IOpenAICompatibleResponseMapper
{
    /// <summary>[EN] Executes the GetPrimaryText operation on the AIKernel public contract surface. [JA] AIKernel の公開契約サーフェスで GetPrimaryText 操作を実行します。</summary>
    string GetPrimaryText(ChatResponse response);

    /// <summary>[EN] Executes the CreateProjection operation on the AIKernel public contract surface. [JA] AIKernel の公開契約サーフェスで CreateProjection 操作を実行します。</summary>
    OpenAICompatibleResponseProjection CreateProjection(
        ChatResponse response,
        string fallbackModelId,
        DateTimeOffset observedAtUtc);
}
