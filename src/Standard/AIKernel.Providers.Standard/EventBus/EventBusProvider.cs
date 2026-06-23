namespace AIKernel.Providers.Standard.EventBus;

using System.Collections.Concurrent;
using AIKernel.Abstractions.Events;
using AIKernel.Common.Results;
using AIKernel.Providers.Standard;

/// <summary>
/// [EN] Standard in-memory event bus provider for AIKernel OS internal events.
/// [JA] AIKernel OS 内部 event 向けの標準 in-memory event bus Provider です。
/// </summary>
public sealed class EventBusProvider : StandardProviderBase, IEventBus
{
    private readonly ConcurrentDictionary<string, List<Subscription>> _handlers = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, string> _subscriptionEvents = new(StringComparer.Ordinal);

    /// <summary>[EN] Initializes the event bus provider. [JA] event bus Provider を初期化します。</summary>
    public EventBusProvider()
        : base("providers.eventbus.memory", "Event Bus Provider", "0.1.3", ["event.publish", "event.subscribe"], ["event"])
    {
    }

    /// <summary>[EN] Subscribes to an event. [JA] event を購読します。</summary>
    public string Subscribe(string eventName, Func<object, Task> handler)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(eventName);
        ArgumentNullException.ThrowIfNull(handler);
        var id = Guid.NewGuid().ToString("N");
        var handlers = _handlers.GetOrAdd(eventName, _ => []);
        lock (handlers)
        {
            handlers.Add(new Subscription(id, handler));
        }

        _subscriptionEvents[id] = eventName;
        return id;
    }

    /// <summary>[EN] Publishes an event to subscribers. [JA] subscriber へ event を発行します。</summary>
    public async Task PublishAsync(string eventName, object payload)
        => await PublishAsync(eventName, payload, CancellationToken.None).ConfigureAwait(false);

    /// <summary>[EN] Documents this public package API member. [JA] PublishAsync を実行します。</summary>
    /// <inheritdoc />
    public async Task PublishAsync(string eventName, object eventData, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(eventName);
        cancellationToken.ThrowIfCancellationRequested();
        var snapshot = Handlers(eventName)
            .Match(
                () => [],
                handlers => SnapshotHandlers(handlers));

        foreach (var subscription in snapshot)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await subscription.Handler(eventData).ConfigureAwait(false);
        }
    }

    /// <summary>[EN] Documents this public package API member. [JA] BroadcastAsync を実行します。</summary>
    /// <inheritdoc />
    public Task BroadcastAsync(string eventName, object eventData, CancellationToken cancellationToken = default)
        => PublishAsync(eventName, eventData, cancellationToken);

    /// <summary>[EN] Documents this public package API member. [JA] Subscribe&lt;T&gt; を実行します。</summary>
    /// <inheritdoc />
    public string Subscribe<T>(string eventName, Func<T, Task> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        return Subscribe(eventName, payload => handler((T)payload));
    }

    /// <summary>[EN] Documents this public package API member. [JA] Unsubscribe を実行します。</summary>
    /// <inheritdoc />
    public bool Unsubscribe(string subscriptionId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(subscriptionId);
        return RemovedSubscriptionEvent(subscriptionId)
            .Bind(Handlers)
            .Match(
                () => false,
                handlers => RemoveHandler(handlers, subscriptionId));
    }

    /// <summary>[EN] Documents this public package API member. [JA] GetSubscriberCount を実行します。</summary>
    /// <inheritdoc />
    public int GetSubscriberCount(string eventName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(eventName);
        return Handlers(eventName).Match(() => 0, CountHandlers);
    }

    private Option<List<Subscription>> Handlers(string eventName)
        => _handlers.TryGetValue(eventName, out var handlers)
            ? Option<List<Subscription>>.Some(handlers)
            : Option<List<Subscription>>.None();

    private Option<string> RemovedSubscriptionEvent(string subscriptionId)
        => _subscriptionEvents.TryRemove(subscriptionId, out var eventName)
            ? Option<string>.Some(eventName)
            : Option<string>.None();

    private static Subscription[] SnapshotHandlers(List<Subscription> handlers)
    {
        lock (handlers)
        {
            return handlers.ToArray();
        }
    }

    private static int CountHandlers(List<Subscription> handlers)
    {
        lock (handlers)
        {
            return handlers.Count;
        }
    }

    private static bool RemoveHandler(List<Subscription> handlers, string subscriptionId)
    {
        lock (handlers)
        {
            return handlers.RemoveAll(subscription => string.Equals(subscription.Id, subscriptionId, StringComparison.Ordinal)) > 0;
        }
    }

    private sealed record Subscription(string Id, Func<object, Task> Handler);
}
