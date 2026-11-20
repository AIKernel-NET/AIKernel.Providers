namespace AIKernel.Providers.Standard.Processes;

using System.Collections.Concurrent;
using AIKernel.Abstractions.Processes;
using AIKernel.Common.Results;
using AIKernel.Providers.Standard;
using CoreProcessInfo = AIKernel.Abstractions.Processes.ProcessInfo;
using CoreProcessState = AIKernel.Abstractions.Processes.ProcessState;

/// <summary>[EN] Logical AIKernel process state. [JA] 論理 AIKernel process state です。</summary>
public enum ProcessState
{
    /// <summary>[EN] Process is starting. [JA] process は起動中です。</summary>
    Starting,
    /// <summary>[EN] Process is running. [JA] process は実行中です。</summary>
    Running,
    /// <summary>[EN] Process is stopped. [JA] process は停止済みです。</summary>
    Stopped,
    /// <summary>[EN] Process failed. [JA] process は失敗しました。</summary>
    Failed
}

/// <summary>
/// [EN] Public logical process snapshot.
/// [JA] 公開 logical process snapshot です。
/// </summary>
public sealed record ProcessInfo(
    string ProcessId,
    string Name,
    ProcessState State,
    DateTimeOffset StartedAtUtc,
    IReadOnlyDictionary<string, string> Metadata);

/// <summary>
/// [EN] Standard provider that supervises AIKernel logical process lifecycle.
/// [JA] AIKernel logical process lifecycle を監視する標準 Provider です。
/// </summary>
public sealed class ProcessSupervisorProvider : StandardProviderBase
{
    private readonly ConcurrentDictionary<string, ProcessInfo> _processes = new(StringComparer.Ordinal);

    /// <summary>[EN] Initializes the process supervisor provider. [JA] process supervisor Provider を初期化します。</summary>
    public ProcessSupervisorProvider()
        : base(
            "providers.process.supervisor",
            "Process Supervisor Provider",
            global::AIKernel.Providers.ProviderBuildVersion.Value,
            ["process.start", "process.list", "process.kill", "process.restart"],
            ["process", "process.info"])
    {
    }

    /// <summary>[EN] Starts a logical process. [JA] logical process を開始します。</summary>
    public ProcessInfo Start(string name, IReadOnlyDictionary<string, string>? metadata = null)
    {
        var id = $"proc-{Guid.NewGuid():N}";
        var info = new ProcessInfo(
            id,
            ProcessSupervisorValidation.RequireProcessName(name),
            ProcessState.Running,
            DateTimeOffset.UtcNow,
            metadata ?? new Dictionary<string, string>());
        _processes[id] = info;
        return info;
    }

    /// <summary>[EN] Lists known logical processes. [JA] 既知の logical process を列挙します。</summary>
    public IReadOnlyList<ProcessInfo> List()
        => _processes.Values.OrderBy(process => process.ProcessId, StringComparer.Ordinal).ToArray();

    /// <summary>[EN] Marks a logical process as stopped. [JA] logical process を停止済みにします。</summary>
    public bool Kill(string processId)
        => Update(processId, ProcessState.Stopped);

    /// <summary>[EN] Restarts a logical process. [JA] logical process を再起動します。</summary>
    public bool Restart(string processId)
        => Update(processId, ProcessState.Running);

    private bool Update(string processId, ProcessState state)
    {
        while (_processes.TryGetValue(processId, out var current))
        {
            var next = current with { State = state };
            if (!_processes.TryUpdate(processId, next, current)) continue;
            if (state == ProcessState.Stopped) TrimStopped();
            return true;
        }
        return false;
    }

    private void TrimStopped()
    {
        foreach (var pair in _processes.Where(p => p.Value.State == ProcessState.Stopped)
            .OrderByDescending(p => p.Value.StartedAtUtc).ThenBy(p => p.Key, StringComparer.Ordinal).Skip(256))
            _processes.TryRemove(pair);
    }


}

/// <summary>
/// [EN] Default process supervisor provider implementing the Core process-supervisor abstraction.
/// [JA] Core process-supervisor 抽象を実装する default process supervisor Provider です。
/// </summary>
public sealed class DefaultProcessSupervisorProvider : StandardProviderBase, IProcessSupervisorProvider, IProcessHost
{
    private readonly ConcurrentDictionary<ProcessId, CoreProcessInfo> _processes = new();

    /// <summary>[EN] Initializes the default process supervisor provider. [JA] default process supervisor Provider を初期化します。</summary>
    public DefaultProcessSupervisorProvider()
        : base(
            "providers.process.supervisor.default",
            "Default Process Supervisor Provider",
            global::AIKernel.Providers.ProviderBuildVersion.Value,
            ["process.start", "process.list", "process.kill", "process.restart"],
            ["process", "process.info"])
    {
    }

    /// <summary>[EN] Starts a logical process snapshot. [JA] logical process snapshot を開始します。</summary>
    public CoreProcessInfo Start(string name, IReadOnlyDictionary<string, string>? metadata = null) => Create(name, metadata, CoreProcessState.Running);

    private CoreProcessInfo Create(string name, IReadOnlyDictionary<string, string>? metadata, CoreProcessState state)
    {
        var id = new ProcessId(Guid.NewGuid());
        var info = new CoreProcessInfo(
            id,
            ProcessSupervisorValidation.RequireProcessName(name),
            state,
            DateTimeOffset.UtcNow,
            metadata ?? new Dictionary<string, string>());
        _processes[id] = info;
        return info;
    }

    /// <summary>[EN] Documents this public package API member. [JA] CreateProcessAsync を実行します。</summary>
    /// <inheritdoc />
    public Task<IProcess> CreateProcessAsync(string name, object? args = null)
    {
        var metadata = args as IReadOnlyDictionary<string, string>;
        var info = Create(name, metadata, CoreProcessState.Starting);
        return Task.FromResult<IProcess>(new StandardManagedProcess(info, UpdateProcessState));
    }

    /// <summary>[EN] Documents this public package API member. [JA] ListAsync を実行します。</summary>
    /// <inheritdoc />
    public Task<CoreProcessInfo[]> ListAsync()
        => Task.FromResult(_processes.Values.OrderBy(process => process.Id.Value).ToArray());

    /// <summary>[EN] Documents this public package API member. [JA] KillAsync を実行します。</summary>
    /// <inheritdoc />
    public Task KillAsync(ProcessId id)
    {
        UpdateCoreProcessState(id, CoreProcessState.Stopped);
        return Task.CompletedTask;
    }

    /// <summary>[EN] Documents this public package API member. [JA] RestartAsync を実行します。</summary>
    /// <inheritdoc />
    public Task RestartAsync(ProcessId id)
    {
        UpdateCoreProcessState(id, CoreProcessState.Running);
        return Task.CompletedTask;
    }

    private Task UpdateProcessState(ProcessId id, CoreProcessState state)
    {
        UpdateCoreProcessState(id, state);
        return Task.CompletedTask;
    }

    private void UpdateCoreProcessState(ProcessId id, CoreProcessState state)
    {
        while (_processes.TryGetValue(id, out var current))
        {
            if (!_processes.TryUpdate(id, current with { State = state }, current)) continue;
            if (state == CoreProcessState.Stopped)
            {
                foreach (var pair in _processes.Where(p => p.Value.State == CoreProcessState.Stopped)
                    .OrderByDescending(p => p.Value.StartedAtUtc).ThenBy(p => p.Key.Value).Skip(256))
                    _processes.TryRemove(pair);
            }
            break;
        }
    }

}

internal static class ProcessSupervisorValidation
{
    /// <summary>
    /// EN: Executes RequireProcessName.
    /// [EN] Documents this public package API member. [JA] RequireProcessName を実行します。
    /// </summary>
    public static string RequireProcessName(string name)
        => ValidateProcessName(name).Match(
            error => throw new ArgumentException(error.Message, nameof(name)),
            value => value);

    private static Result<string> ValidateProcessName(string name)
        => string.IsNullOrWhiteSpace(name)
            ? Result<string>.Fail("Process name is required. ErrorCode=PROCESS_NAME_REQUIRED")
            : Result<string>.Success(name);
}

/// <summary>
/// [EN] In-memory process handle managed by the default process supervisor.
/// [JA] default process supervisor によって管理される in-memory process handle です。
/// </summary>
public sealed class StandardManagedProcess : IProcess
{
    private readonly Func<ProcessId, CoreProcessState, Task> _update;

    /// <summary>
    /// [EN] Initializes a managed process handle from a process snapshot.
    /// [JA] process snapshot から managed process handle を初期化します。
    /// </summary>
    public StandardManagedProcess(CoreProcessInfo info, Func<ProcessId, CoreProcessState, Task> update)
    {
        ArgumentNullException.ThrowIfNull(update);
        Id = info.Id;
        Name = info.Name;
        State = info.State;
        _update = update;
    }

    /// <summary>[EN] Process identifier. [JA] process 識別子です。</summary>
    public ProcessId Id { get; }

    /// <summary>[EN] Process name. [JA] process 名です。</summary>
    public string Name { get; }

    /// <summary>[EN] Current process state. [JA] 現在の process state です。</summary>
    public CoreProcessState State { get; private set; }

    /// <summary>[EN] Marks the process as running. [JA] process を running として mark します。</summary>
    public async Task StartAsync()
    {
        State = CoreProcessState.Running;
        await _update(Id, State).ConfigureAwait(false);
    }

    /// <summary>[EN] Marks the process as stopped. [JA] process を stopped として mark します。</summary>
    public async Task StopAsync()
    {
        State = CoreProcessState.Stopped;
        await _update(Id, State).ConfigureAwait(false);
    }
}
