namespace AIKernel.Providers.Substrate.Tests;

public sealed class ManifestCancellationTests
{
    [Theory]
    [InlineData("operation")]
    [InlineData("task")]
    [InlineData("derived")]
    [InlineData("linked")]
    public async Task StreamCancellationIsPropagatedWithoutAddingDependencies(string kind)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken); linked.Cancel();
        var token = kind == "linked" ? linked.Token : CancellationToken.None;
        OperationCanceledException expected = kind switch { "task" or "linked" => new TaskCanceledException("test", null, token), "derived" => new DerivedCancellation(token), _ => new("test", token) };
        await using var stream = new FailingStream(expected);
        var pending = new ProviderManifestLoader().LoadStreamAsync(stream, "test", TestContext.Current.CancellationToken).AsTask();
        var error = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
        Assert.Same(expected, error); Assert.Equal(token, error.CancellationToken); Assert.True(pending.IsCanceled);
    }
    [Fact]
    public async Task FileLoadHonorsAnAlreadyCanceledLinkedToken()
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken); linked.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new ProviderManifestLoader().LoadFileAsync("unused-b4.json", linked.Token).AsTask());
    }
    [Fact]
    public async Task StreamIoFailureKeepsStructuredFailure()
    {
        await using var stream = new FailingStream(new IOException("test"));
        var result = await new ProviderManifestLoader().LoadStreamAsync(stream, "test", TestContext.Current.CancellationToken);
        Assert.False(result.Succeeded);
    }
    private sealed class DerivedCancellation(CancellationToken token) : OperationCanceledException("test", token);
    private sealed class FailingStream(Exception error) : MemoryStream
    {
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) => ValueTask.FromException<int>(error);
    }
}
