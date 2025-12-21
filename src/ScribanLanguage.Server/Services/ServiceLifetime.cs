using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using Microsoft.Extensions.Hosting;

namespace ScribanLanguage.Services;

public interface IServiceLifetime
{
    IServiceProvider Services { get; }
    CancellationToken StoppingToken { get; }
    void Exit(Exception? error = null);
    void Register(IDisposable? disposable);
    void Register(IAsyncDisposable? disposable);
    void RegisterAttached(ILifetimeMember owner, IDisposable? disposable);
    void RegisterAttached(ILifetimeMember owner, IAsyncDisposable? disposable);
}

public interface ILifetimeMember
{
    public IServiceLifetime Lifetime { get; init; }

    protected ServiceLifetime.AttachedDisposables Disposables { get; init; }

    public static class Attached
    {
        public static ServiceLifetime.AttachedDisposables GetDisposables(ILifetimeMember? lifetimeMember) =>
            lifetimeMember?.Disposables ?? default;
    }
}

public abstract class LifetimeObject() : ILifetimeMember, IAsyncDisposable
{
    private ServiceLifetime.AttachedDisposables? _disposables;

    [SetsRequiredMembers]
    public LifetimeObject(IServiceLifetime lifetime) : this()
    {
        Lifetime = lifetime;
    }

    [IgnoreDataMember, DebuggerHidden]
    public required IServiceLifetime Lifetime
    {
        get;
        init
        {
            field = value;
            value.Register(this);
        }
    }

    ServiceLifetime.AttachedDisposables ILifetimeMember.Disposables
    {
        get => _disposables ??= new([]);
        init => _disposables = value;
    }

    ValueTask IAsyncDisposable.DisposeAsync()
    {
        GC.SuppressFinalize(this);
        return _disposables?.DisposeAsync() ?? default;
    }
}

public abstract record LifetimeRecord() : ILifetimeMember, IAsyncDisposable
{
    private ServiceLifetime.AttachedDisposables? _disposables;

    protected LifetimeRecord(LifetimeRecord original)
    {
        Lifetime = original.Lifetime;
        // move state & feed ownership to the new instance
        _disposables = original._disposables;
        original._disposables = null;
    }

    [SetsRequiredMembers]
    public LifetimeRecord(IServiceLifetime lifetime) : this()
    {
        Lifetime = lifetime;
    }

    [IgnoreDataMember, DebuggerHidden]
    public required IServiceLifetime Lifetime
    {
        get;
        init
        {
            field = value;
            value.Register(this);
        }
    }

    ServiceLifetime.AttachedDisposables ILifetimeMember.Disposables
    {
        get => _disposables ??= new([]);
        init => _disposables = value;
    }

    ValueTask IAsyncDisposable.DisposeAsync()
    {
        GC.SuppressFinalize(this);
        return _disposables?.DisposeAsync() ?? default;
    }
}

public static class ServiceLifetime
{
    public static IServiceCollection AddServiceLifetime(this IServiceCollection services)
    {
        services
            .AddSingleton<ServiceLifetimeHostedService>()
            .AddHostedService<ServiceLifetimeHostedService>(sp => sp.GetRequiredService<ServiceLifetimeHostedService>())
            .AddSingleton<IServiceLifetime>(sp => sp.GetRequiredService<ServiceLifetimeHostedService>());
        return services;
    }

    public readonly struct AttachedDisposables(List<GCHandle> attached)
    {
        private readonly List<GCHandle>? _attached = attached;

        public void Attach(IDisposable? disposable)
        {
            // ReSharper disable once InconsistentlySynchronizedField
            if (disposable is null || _attached is null) return;
            lock (_attached)
            {
                _attached.Add(GCHandle.Alloc(disposable, GCHandleType.Weak));
            }
        }

        public void Attach(IAsyncDisposable? disposable)
        {
            // ReSharper disable once InconsistentlySynchronizedField
            if (disposable is null || _attached is null) return;
            lock (_attached)
            {
                _attached.Add(GCHandle.Alloc(disposable, GCHandleType.Weak));
            }
        }

        private IEnumerable<object> Drain()
        {
            // ReSharper disable once InconsistentlySynchronizedField
            if (_attached is null) yield break;
            lock (_attached)
            {
                for (var index = 0; index < _attached.Count; index++)
                {
                    ref var h = ref CollectionsMarshal.AsSpan(_attached)[index];
                    var result = h.IsAllocated ? h.Target : null;
                    if (h.IsAllocated) h.Free();
                    if (result is not null)
                    {
                        yield return result;
                    }
                }

                _attached.Clear();
            }
        }

        public async ValueTask DisposeAsync()
        {
            List<Exception> errors = [];
            List<Task> tasks = [];
            foreach (var item in Drain())
            {
                try
                {
                    if (item is IAsyncDisposable async)
                    {
                        var maybeTask = async.DisposeAsync();
                        if (!maybeTask.IsCompletedSuccessfully)
                        {
                            tasks.Add(maybeTask.AsTask());
                        }
                    }
                    else
                    {
                        ((IDisposable)item).Dispose();
                    }
                }
                catch (Exception ex)
                {
                    errors.Add(ex);
                }
            }

            try
            {
                await Task.WhenAll(tasks).ConfigureAwait(false);
            }
            catch (AggregateException agg)
            {
                errors.AddRange(agg.InnerExceptions);
            }
            catch (Exception ex)
            {
                errors.Add(ex);
            }

            if (errors.Count != 0)
            {
                throw new AggregateException("Error disposing one or more disposables", errors);
            }
        }
    }

    private sealed class ServiceLifetimeHostedService(IServiceProvider sp)
        : IServiceLifetime, IAsyncDisposable, IHostedService
    {
        private CancellationTokenSource? _cts = new();
        private readonly ConcurrentQueue<GCHandle> _weakDisposables = new();
        private long _lastTrimTs;

        public IServiceProvider Services { get; } = sp;

        public CancellationToken StoppingToken => _cts?.Token ?? new CancellationToken(true);

        public void Exit(Exception? error = null)
        {
            _ = Dispose(error);
        }

        public void Register(IDisposable? disposable)
        {
            if (disposable is null) return;
            TrimWeakDisposables();
            _weakDisposables.Enqueue(GCHandle.Alloc(disposable, GCHandleType.Weak));
        }

        public void Register(IAsyncDisposable? disposable)
        {
            if (disposable is null) return;
            TrimWeakDisposables();
            _weakDisposables.Enqueue(GCHandle.Alloc(disposable, GCHandleType.Weak));
        }

        public void RegisterAttached(ILifetimeMember owner, IDisposable? disposable)
        {
            ILifetimeMember.Attached.GetDisposables(owner).Attach(disposable);
        }

        public void RegisterAttached(ILifetimeMember owner, IAsyncDisposable? disposable)
        {
            ILifetimeMember.Attached.GetDisposables(owner).Attach(disposable);
        }

        public async ValueTask DisposeAsync()
        {
            await Dispose(null).ConfigureAwait(false);
        }

        private void TrimWeakDisposables()
        {
            var now = Stopwatch.GetTimestamp();
            var last = Interlocked.Exchange(ref _lastTrimTs, now);
            if (Stopwatch.GetElapsedTime(last, now) < TimeSpan.FromSeconds(5))
            {
                Volatile.Write(ref _lastTrimTs, last);
                return;
            }

            var maxCount = _weakDisposables.Count;
            for (int i = 0; i < maxCount && _weakDisposables.TryDequeue(out var handle); i++)
            {
                if (!handle.IsAllocated) continue;
                if (handle.Target is not null)
                {
                    _weakDisposables.Enqueue(handle);
                }
                else
                {
                    handle.Free();
                }
            }
        }

        private async Task Dispose(Exception? error)
        {
            if (Interlocked.Exchange(ref _cts, null) is not { } cts)
            {
                return;
            }

            await cts.CancelAsync().ConfigureAwait(false);
            List<Exception> errors = new();
            List<Task> disposeTasks = new();
            if (error is not null)
            {
                errors.Add(error);
            }

            while (_weakDisposables.TryDequeue(out var handle))
            {
                if (handle is { IsAllocated: true, Target: { } disposable })
                {
                    handle.Free();
                    SafeDispose(disposable);
                }
            }

            try
            {
                await Task.WhenAll(disposeTasks).ConfigureAwait(false);
            }
            catch (AggregateException agg)
            {
                errors.AddRange(agg.InnerExceptions);
            }
            catch (Exception ex)
            {
                errors.Add(ex);
            }

            try
            {
                cts.Dispose();
            }
            catch (Exception ex)
            {
                errors.Add(ex);
            }


            if (errors.Count != 0)
            {
                throw new AggregateException("Error disposing one or more disposables", errors);
            }

            void SafeDispose(object disposable)
            {
                try
                {
                    if (disposable is IAsyncDisposable a)
                    {
                        var maybeTask = a.DisposeAsync();
                        if (!maybeTask.IsCompletedSuccessfully)
                        {
                            disposeTasks.Add(maybeTask.AsTask());
                        }
                    }
                    else
                    {
                        ((IDisposable)disposable).Dispose();
                    }
                }
                catch (Exception ex)
                {
                    errors.Add(ex);
                }
            }
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            return Dispose(null);
        }
    }
}