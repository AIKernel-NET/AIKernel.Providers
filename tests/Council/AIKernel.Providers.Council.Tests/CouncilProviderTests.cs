namespace AIKernel.Providers.Council.Tests;

using AIKernel.Enums.Governance;
using AIKernel.Providers.Council.Contracts;
using AIKernel.Providers.Council.DependencyInjection;
using AIKernel.Providers.Council.Dimensions;
using AIKernel.Providers.Council.Envelopes;
using AIKernel.Providers.Council.Providers;
using AIKernel.Providers.Council.Routing;
using Microsoft.Extensions.DependencyInjection;

public sealed class CouncilProviderTests
{
    [Fact]
    public async Task EvaluateAsync_ConfiguredStub_ReturnsUnknownVoteOnly()
    {
        var provider = new LogosSemanticEvaluationProvider();

        var result = await provider.EvaluateAsync(new CouncilSemanticEvaluationRequest
        {
            OperationId = "op",
            StepId = "step",
            CouncilKind = CouncilKind.Logos
        }, TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.Equal(CouncilKind.Logos, result.CouncilKind);
        Assert.Equal(SemanticEvaluationStatus.Inconclusive, result.SemanticResult?.Status);
        Assert.Equal(CouncilVoteValue.Unknown, result.SemanticResult?.VoteValue);
        Assert.Equal(CouncilVoteValue.Unknown, result.SemanticResult?.ProposedVoteValue);
        Assert.Null(result.ErrorCode);
        Assert.Equal(
            CouncilSemanticDimensionKeys.LogosMinimumKeys,
            result.SemanticResult?.Dimensions.Keys.ToArray());
    }

    [Fact]
    public async Task EvaluateAsync_CouncilMismatch_ReturnsStructuredFailure()
    {
        var provider = new EthosSemanticEvaluationProvider();

        var result = await provider.EvaluateAsync(new CouncilSemanticEvaluationRequest
        {
            OperationId = "op",
            StepId = "step",
            CouncilKind = CouncilKind.Pathos
        }, TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.Equal("COUNCIL_PROVIDER_KIND_MISMATCH", result.ErrorCode);
        Assert.Null(result.SemanticResult);
    }

    [Fact]
    public void CouncilProviderResolutionPolicy_Logos_CreatesSemanticCapability()
    {
        var policy = new CouncilProviderResolutionPolicy().Create(CouncilKind.Logos);

        Assert.Equal("ctg.council.logos", policy.RequiredCapability);
    }

    [Fact]
    public void AddAIKernelCouncilProviders_RegistersThreeSemanticProviders()
    {
        using var provider = new ServiceCollection()
            .AddAIKernelCouncilProviders()
            .BuildServiceProvider();

        var providers = provider.GetServices<ICouncilSemanticEvaluationProvider>().ToArray();

        Assert.Contains(providers, item => item.CouncilKind == CouncilKind.Logos);
        Assert.Contains(providers, item => item.CouncilKind == CouncilKind.Ethos);
        Assert.Contains(providers, item => item.CouncilKind == CouncilKind.Pathos);
    }

    [Fact]
    public void ProviderSemanticResult_PublicSurface_ExposesSemanticMaterialOnly()
    {
        var propertyNames = typeof(ProviderSemanticResult)
            .GetProperties()
            .Select(property => property.Name)
            .Concat(typeof(CouncilSemanticEvaluationResult)
                .GetProperties()
                .Select(property => property.Name))
            .ToArray();

        Assert.Contains(nameof(ProviderSemanticResult.VoteValue), propertyNames);
        Assert.Contains(nameof(ProviderSemanticResult.ProposedVoteValue), propertyNames);
        Assert.Contains(nameof(ProviderSemanticResult.Status), propertyNames);
        Assert.Contains(nameof(ProviderSemanticResult.Rationale), propertyNames);
        Assert.Contains(nameof(ProviderSemanticResult.EvidenceRefs), propertyNames);
        Assert.Contains(nameof(ProviderSemanticResult.Dimensions), propertyNames);
        Assert.Contains(nameof(ProviderSemanticResult.Diagnostics), propertyNames);
        Assert.Contains(nameof(CouncilSemanticEvaluationResult.SemanticResult), propertyNames);
    }

    [Fact]
    public void ProviderSemanticResult_PublicSurface_ContainsNoGateDecisionTypes()
    {
        var propertyTypes = typeof(ProviderSemanticResult)
            .GetProperties()
            .Select(property => property.PropertyType.FullName ?? property.PropertyType.Name)
            .ToArray();

        Assert.DoesNotContain(propertyTypes, type => type.Contains("GateDecisionKind", StringComparison.Ordinal));
        Assert.DoesNotContain(propertyTypes, type => type.Contains("TrajectoryGateDecisionKind", StringComparison.Ordinal));
        Assert.DoesNotContain(propertyTypes, type => type.Contains("GateInput", StringComparison.Ordinal));
    }

    [Fact]
    public void ProviderSemanticResult_ContinuousCarriers_RemainSemanticMaterial()
    {
        var result = new ProviderSemanticResult
        {
            Confidence = 0.5,
            RiskScore = 0.25
        };

        Assert.Equal(0.5, result.Confidence);
        Assert.Equal(0.25, result.RiskScore);
    }

    [Theory]
    [InlineData(CouncilKind.Logos, "logos.logical_consistency", "logos.evidence_grounding", "logos.causal_coherence")]
    [InlineData(CouncilKind.Ethos, "ethos.safety_alignment", "ethos.permission_alignment", "ethos.reversibility")]
    [InlineData(CouncilKind.Pathos, "pathos.context_alignment", "pathos.user_intent_alignment", "pathos.impact_alignment")]
    public void CouncilSemanticDimensionKeys_MinimumKeys_AreStableByCouncil(
        CouncilKind councilKind,
        string first,
        string second,
        string third)
    {
        var keys = CouncilSemanticDimensionKeys.GetMinimumKeys(councilKind);

        Assert.Equal([first, second, third], keys);
    }

    [Fact]
    public void CouncilSemanticDimensionKeys_NotEvaluatedDimensions_ContainMinimumKeysOnly()
    {
        var dimensions = CouncilSemanticDimensionKeys.CreateNotEvaluatedDimensions(CouncilKind.Ethos);

        Assert.Equal(CouncilSemanticDimensionKeys.EthosMinimumKeys, dimensions.Keys.ToArray());
        Assert.All(dimensions.Values, value => Assert.Equal(CouncilSemanticDimensionKeys.NotEvaluated, value));
    }
}
