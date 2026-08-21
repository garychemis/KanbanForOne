using System.Threading;

namespace KanbanForOne.Services;

/// <summary>
/// Coordinates application instances in the current Windows session.
/// </summary>
public sealed class SingleInstanceManager : IDisposable
{
    private readonly Mutex _instanceMutex;
    private readonly EventWaitHandle _activationEvent;
    private readonly RegisteredWaitHandle? _activationWait;
    private readonly bool _ownsMutex;
    private bool _disposed;

    public SingleInstanceManager(string instanceName, Action activationRequested)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(instanceName);
        ArgumentNullException.ThrowIfNull(activationRequested);

        var safeName = instanceName.Replace('\\', '_');
        _activationEvent = new EventWaitHandle(
            false,
            EventResetMode.AutoReset,
            $"Local\\{safeName}.Activate");
        _instanceMutex = new Mutex(
            true,
            $"Local\\{safeName}.Mutex",
            out _ownsMutex);

        if (_ownsMutex)
        {
            _activationWait = ThreadPool.RegisterWaitForSingleObject(
                _activationEvent,
                (_, timedOut) =>
                {
                    if (!timedOut)
                    {
                        activationRequested();
                    }
                },
                null,
                Timeout.Infinite,
                false);
        }
    }

    public bool IsPrimaryInstance => _ownsMutex;

    public void NotifyPrimaryInstance()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _activationEvent.Set();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _activationWait?.Unregister(null);

        if (_ownsMutex)
        {
            _instanceMutex.ReleaseMutex();
        }

        _instanceMutex.Dispose();
        _activationEvent.Dispose();
    }
}
