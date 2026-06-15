namespace AIKernel.Providers.Perception.Tests;

using AIKernel.Providers.Perception.DependencyInjection;
using AIKernel.Providers.Perception.Routing;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// [EN] Tests perception provider substrate routing contracts.
/// [JA] perception Provider substrate routing contract をテストします。
/// </summary>
public sealed class PerceptionProviderResolutionPolicyTests
{
    /// <summary>
    /// [EN] Verifies frame policy capability remains deterministic.
    /// [JA] frame policy capability が deterministic に維持されることを検証します。
    /// </summary>
    [Fact]
    public void CreateFramePolicy_DefaultRequest_UsesFrameCapability()
    {
        var policy = new PerceptionProviderResolutionPolicy().CreateFramePolicy();

        Assert.Equal("perception.frame", policy.RequiredCapability);
    }

    /// <summary>
    /// [EN] Verifies service registration exposes routing helpers without concrete providers.
    /// [JA] service registration が concrete Provider なしで routing helper を公開することを検証します。
    /// </summary>
    [Fact]
    public void AddAIKernelPerceptionProviderSubstrate_DefaultServices_RegistersRoutingPolicy()
    {
        var services = new ServiceCollection();

        services.AddAIKernelPerceptionProviderSubstrate();

        using var provider = services.BuildServiceProvider();
        Assert.NotNull(provider.GetService<PerceptionProviderResolutionPolicy>());
    }
}
