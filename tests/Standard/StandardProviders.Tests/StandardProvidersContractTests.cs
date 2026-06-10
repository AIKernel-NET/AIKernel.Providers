using AIKernel.Abstractions.Compute;
using AIKernel.Abstractions.Logging;
using AIKernel.Abstractions.Network;
using AIKernel.Abstractions.Processes;
using AIKernel.Abstractions.Providers;
using AIKernel.Providers.Standard.Compute;
using AIKernel.Providers.Standard.DependencyInjection;
using AIKernel.Providers.Standard.EventBus;
using AIKernel.Providers.Standard.FileSystem;
using AIKernel.Providers.Standard.Logging;
using AIKernel.Providers.Standard.Network;
using AIKernel.Providers.Standard.Processes;
using AIKernel.Providers.Standard.Profiler;
using AIKernel.Providers.Standard.Scheduler;
using AIKernel.Vfs;
using Microsoft.Extensions.DependencyInjection;
using System.Text;
using StandardProcessState = AIKernel.Providers.Standard.Processes.ProcessState;

namespace AIKernel.Providers.Tests;

public sealed class StandardProvidersContractTests
{
    [Fact]
    public async Task StandardProviders_ExposeProviderLifecycle()
    {
        IProvider[] providers =
        [
            new CpuComputeProvider(),
            new MemoryFileSystemProvider(),
            new PhysicalFileSystemProvider(),
            new ConsoleLoggingProvider(),
            new ProcessSupervisorProvider(),
            new EventBusProvider(),
            new NetworkProvider(new HttpClient(new StaticHttpHandler())),
            new SchedulerProvider(),
            new ProfilerProvider()
        ];

        foreach (var provider in providers)
        {
            Assert.False(await provider.IsAvailableAsync());
            await provider.InitializeAsync();
            Assert.True(await provider.IsAvailableAsync());
            Assert.True(provider.GetCapabilities().SupportedOperations.Count > 0);
            await provider.ShutdownAsync();
            Assert.False(await provider.IsAvailableAsync());
        }
    }

    [Fact]
    public void CpuComputeProvider_VectorOperationsAreDeterministic()
    {
        var provider = new CpuComputeProvider();

        Assert.Equal([3.0f, 5.0f, 7.0f], provider.AddVectors([1.0f, 2.0f, 3.0f], [2.0f, 3.0f, 4.0f]));
        Assert.Equal(20.0f, provider.Dot([1.0f, 2.0f, 3.0f], [2.0f, 3.0f, 4.0f]));
        Assert.IsAssignableFrom<IComputeProvider>(provider);
    }

    [Fact]
    public void CpuComputeProvider_TryOperationsFailClosed()
    {
        var provider = new CpuComputeProvider();

        var result = provider.TryAddVectors([1.0f], [1.0f, 2.0f]);

        Assert.True(result.IsFailure);
        Assert.Contains("CPU_VECTOR_LENGTH_MISMATCH", result.Error!.Message);
    }

    [Fact]
    public void MemoryFileSystemProvider_ReadsAndListsFiles()
    {
        var provider = new MemoryFileSystemProvider();

        provider.WriteText("/process/config.txt", "mode=test");

        Assert.True(provider.Exists("/process/config.txt"));
        Assert.Equal("mode=test", provider.ReadText("/process/config.txt"));
        Assert.Equal(["/process/config.txt"], provider.List("/process"));
        Assert.IsAssignableFrom<IFileSystemProvider>(provider);
    }

    [Fact]
    public void MemoryFileSystemProvider_TryReadTextReturnsFailureForMissingFile()
    {
        var provider = new MemoryFileSystemProvider();

        var result = provider.TryReadText("/missing.txt");

        Assert.True(result.IsFailure);
        Assert.Contains("FS_MEMORY_FILE_NOT_FOUND", result.Error!.Message);
    }

    [Fact]
    public async Task StandardProviders_ImplementCoreOsAbstractions()
    {
        var compute = new CpuComputeProvider();
        var fileSystem = new MemoryFileSystemProvider();
        var logger = new ConsoleLoggingProvider();
        var supervisor = new DefaultProcessSupervisorProvider();
        var network = new HttpNetworkProvider(new HttpClient(new StaticHttpHandler()));

        Assert.True(((IComputeProvider)compute).IsAvailable());
        ((ILoggingProvider)logger).Log(LogLevel.Information, "standard provider test");

        var process = await supervisor.CreateProcessAsync("bonsai");
        await process.StartAsync();
        Assert.Single(await ((IProcessSupervisorProvider)supervisor).ListAsync());

        var response = await ((INetworkProvider)network).HttpGetAsync("https://example.test/");
        Assert.Equal(200, response.StatusCode);
        Assert.Equal("ok", Encoding.UTF8.GetString(response.Body));

        Assert.IsAssignableFrom<IFileSystemProvider>(fileSystem);
    }

    [Fact]
    public void StandardProviderDependencyInjection_RegistersCoreOsAbstractions()
    {
        using var provider = new ServiceCollection()
            .AddAIKernelStandardProviders()
            .BuildServiceProvider();

        Assert.NotNull(provider.GetRequiredService<IComputeProvider>());
        Assert.NotNull(provider.GetRequiredService<IFileSystemProvider>());
        Assert.NotNull(provider.GetRequiredService<ILoggingProvider>());
        Assert.NotNull(provider.GetRequiredService<IProcessSupervisorProvider>());
        Assert.NotNull(provider.GetRequiredService<INetworkProvider>());
    }

    [Fact]
    public void ProcessSupervisorProvider_ManagesLogicalProcesses()
    {
        var provider = new ProcessSupervisorProvider();
        var process = provider.Start("sample");

        Assert.Equal(StandardProcessState.Running, process.State);
        Assert.True(provider.Kill(process.ProcessId));
        Assert.Equal(StandardProcessState.Stopped, provider.List().Single().State);
        Assert.True(provider.Restart(process.ProcessId));
        Assert.Equal(StandardProcessState.Running, provider.List().Single().State);
    }

    [Fact]
    public async Task EventBusProvider_PublishesEvents()
    {
        var provider = new EventBusProvider();
        var received = "";

        provider.Subscribe("process.metric.low", payload =>
        {
            received = (string)payload;
            return Task.CompletedTask;
        });

        await provider.PublishAsync("process.metric.low", "inject-action", TestContext.Current.CancellationToken);

        Assert.Equal("inject-action", received);
    }

    [Fact]
    public void SchedulerAndProfiler_ExposeOperationalSnapshots()
    {
        var scheduler = new SchedulerProvider();
        var profiler = new ProfilerProvider();

        var job = scheduler.Add("bonsai", TimeSpan.FromSeconds(1));
        var snapshot = profiler.Capture();

        Assert.Equal(job, scheduler.List().Single());
        Assert.True(snapshot.ManagedMemoryBytes > 0);
    }

    private sealed class StaticHttpHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
            => Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent("ok")
            });
    }
}
