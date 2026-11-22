using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AIKernel.Abstractions.Hosting;
using AIKernel.Dtos.Hosting;

namespace AIKernel.Providers.Distributed;

public sealed class HeartbeatMonitor(IConnectionProfileStore profiles, IConnectionClient client,
    TimeSpan? interval = null, TimeSpan? timeout = null) : IHeartbeatProvider
{
    private readonly ConcurrentDictionary<Guid, HeartbeatObservation> observations = new();
    public event Action<HeartbeatObservation>? Changed;
    public HeartbeatObservation Get(Guid serverId) => observations.GetValueOrDefault(serverId)
        ?? new(serverId, ConnectionState.Unchecked, null, null);

    public async Task MonitorAsync(CancellationToken cancellationToken)
    {
        var wake = System.Threading.Channels.Channel.CreateBounded<bool>(1);
        void Wake() => wake.Writer.TryWrite(true);
        profiles.Changed += Wake;
        try
        {
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                while (wake.Reader.TryRead(out _)) { }
                await Parallel.ForEachAsync(profiles.List(), cancellationToken, CheckAsync);
                using var delay = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                delay.CancelAfter(interval ?? TimeSpan.FromSeconds(15));
                try { await wake.Reader.WaitToReadAsync(delay.Token); }
                catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) { }
            }
        }
        finally { profiles.Changed -= Wake; }
    }

    private async ValueTask CheckAsync(ConnectionProfile profile, CancellationToken ct)
    {
        var prior = Get(profile.ServerId);
        if (prior.State == ConnectionState.Unchecked) Set(prior with { State = ConnectionState.Connecting });
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
        deadline.CancelAfter(timeout ?? TimeSpan.FromSeconds(4));
        ConnectionState state;
        try
        {
            using var response = await client.SendAsync(profile.ServerId, HttpMethod.Get, "heartbeat", cancellationToken: deadline.Token);
            if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden) state = ConnectionState.Unauthorized;
            else if (!response.IsSuccessStatusCode) state = ConnectionState.Unavailable;
            else
            {
                var beat = await response.Content.ReadFromJsonAsync<Heartbeat>(PrivateFiles.Json, deadline.Token);
                state = beat == null || !Guid.TryParseExact(beat.ServerId, "N", out var id) || id != profile.ServerId ||
                    beat.Service != profile.Service ? ConnectionState.IdentityMismatch :
                    beat.Status == "online" ? ConnectionState.Online : ConnectionState.Unavailable;
            }
        }
        catch (Exception ex) when (!ct.IsCancellationRequested && ex is HttpRequestException or IOException or OperationCanceledException or JsonException)
        { state = ConnectionState.Unavailable; }
        var now = DateTimeOffset.UtcNow;
        if (profiles.List().Contains(profile))
            Set(new(profile.ServerId, state, now, state == ConnectionState.Online ? now : prior.LastSuccess));
    }

    private void Set(HeartbeatObservation value)
    {
        observations[value.ServerId] = value;
        Changed?.Invoke(value);
    }
}
