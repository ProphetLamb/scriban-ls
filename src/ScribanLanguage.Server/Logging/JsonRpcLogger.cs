using System.Runtime.InteropServices;
using System.Threading.Channels;
using Microsoft.VisualStudio.LanguageServer.Protocol;
using Newtonsoft.Json;
using ScribanLanguage.Services;
using ScribanLanguage.Workspaces;
using StreamJsonRpc;

namespace ScribanLanguage.Logging;

[method: SetsRequiredMembers]
public sealed class JsonRpcLoggerProvider(
    JsonRpc rpc,
    Workspace workspace)
    : LifetimeObject(workspace.Lifetime), ILoggerProvider
{
    private readonly Channel<LogEntry> _messages =
        Channel.CreateBounded<LogEntry>(new BoundedChannelOptions(1024)
            { FullMode = BoundedChannelFullMode.DropOldest });

    [field: AllowNull] private Task DrainTask => field ??= CreateDrain(Lifetime.StoppingToken);

    private async Task CreateDrain(CancellationToken cancellationToken)
    {
        await MinimumLogLevelSource.Read(cancellationToken).ConfigureAwait(false);
        await foreach (var entry in _messages.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
        {
            try
            {
                var message = HostLogLevel <= LogLevel.Trace && !entry.Verbose.IsEmpty()
                    ? $"{entry.Message}{Environment.NewLine}{entry.Verbose}"
                    : entry.Message;
                await rpc.NotifyWithParameterObjectAsync(Methods.WindowLogMessageName, new LogMessageParams
                {
                    MessageType = LogLevelToMessageType(entry.LogLevel),
                    Message = message,
                }).ConfigureAwait(false);
            }
            catch
            {
                // ignore
            }

            // delay at least 1ms before sending the next message, to reduce pressure on the rpc channel
            await Task.Delay(1, cancellationToken).ConfigureAwait(false);
        }
    }

    public ILogger CreateLogger(string categoryName)
    {
        var logger = new JsonRpcLogger(this);
        logger.RootScope(categoryName);
        return logger;
    }

    [field: AllowNull]
    private IState<LogLevel> MinimumLogLevelSource => field ??= State
        .Async(this, workspace.Host.Select(x => x.LogLevel))
        .ForEach(static (logLevel, self) => self.HostLogLevel = logLevel, this);

    public LogLevel HostLogLevel { get; private set; }

    public void Send(LogLevel level, string message, string? verbose)
    {
        _ = DrainTask;
        _messages.Writer.TryWrite(new(level, message, verbose));
    }

    private static MessageType LogLevelToMessageType(LogLevel level)
    {
        return level switch
        {
            <= LogLevel.Debug => MessageType.Log,
            LogLevel.Information => MessageType.Info,
            LogLevel.Warning => MessageType.Warning,
            >= LogLevel.Error => MessageType.Error
        };
    }


    public static LogLevel TraceSettingToLogLevel(TraceSetting traceSetting)
    {
        return traceSetting switch
        {
            TraceSetting.Verbose => LogLevel.Trace,
            TraceSetting.Messages => LogLevel.Debug,
            _ => LogLevel.Information,
        };
    }

    public void Dispose()
    {
        _messages.Writer.TryComplete();
    }

    private readonly struct LogEntry(LogLevel logLevel, string message, string? verbose)
    {
        public LogLevel LogLevel { get; } = logLevel;
        public string Message { get; } = message;
        public string? Verbose { get; } = verbose;
    }
}

public sealed class JsonRpcLogger : ILogger
{
    private GCHandle _self;

    // everything parent of root is protected
    private Scope? _root;

    // everything between parent and root is disposable
    private Scope? _current;
    private readonly JsonRpcLoggerProvider _provider;

    public JsonRpcLogger(JsonRpcLoggerProvider provider)
    {
        _self = GCHandle.Alloc(this, GCHandleType.Weak);
        _provider = provider;
    }

    ~JsonRpcLogger()
    {
        _self.Free();
    }


    public IDisposable BeginScope<TState>(TState state) where TState : notnull
    {
        return BeginScope(_root, state);
    }

    public void RootScope<TState>(TState state) where TState : notnull
    {
        var text = SerializeState(state, null, null);
        var scope = _root = InsertState(_root, text);
        if (ReferenceEquals(_root, _current))
        {
            _current = scope;
        }
    }

    private Scope BeginScope<TState>(Scope? parent, TState state)
    {
        var text = SerializeState(state, null, null);
        var scope = InsertState(parent, text);
        if (ReferenceEquals(parent, _current))
        {
            _current = scope;
        }

        return scope;
    }

    private Scope InsertState(Scope? parent, string text)
    {
        Scope scope = new(_self, text);
        scope.Parent = parent;
        return scope;
    }

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
        Func<TState, Exception?, string>? formatter)
    {
        if (!IsEnabled(logLevel)) return;
        var text = SerializeState(state, exception, formatter);
        var message = Format(_current, eventId, text);
        _provider.Send(logLevel, message, exception?.ToString());
    }

    private void Log<TState>(Scope scope, LogLevel logLevel, EventId eventId, TState state,
        Exception? exception,
        Func<TState, Exception?, string>? formatter)
    {
        if (!IsEnabled(logLevel)) return;
        var text = SerializeState(state, exception, formatter);
        var message = Format(scope, eventId, text);
        _provider.Send(logLevel, message, exception?.ToString());
    }

    private static string Format(Scope? scope, EventId eventId, string text)
    {
        StringBuilder message = new(2048);
        Stack<string> scopes = new();
        while (scope is not null)
        {
            var parent = scope.Parent;
            if (scope.Disposed)
            {
                scope.Parent = null;
            }
            else
            {
                scopes.Push(scope.Text);
            }

            scope = parent;
        }

        while (scopes.TryPop(out var s))
        {
            message.Append(s).Append('|');
        }

        message.Length -= message.Length > 0 ? 1 : 0;
        if (eventId != default)
        {
            message.Append('|').Append(eventId);
        }

        message.Append(": ").Append(text);

        return message.ToString();
    }

    private static string SerializeState<TState>(TState state, Exception? exception,
        Func<TState, Exception?, string>? formatter)
    {
        string? text = null;
        try
        {
            text = formatter?.Invoke(state, exception) ?? state?.ToString();
        }
        catch
        {
            // ignore
        }

        string typeString;
        try
        {
            var t = state?.GetType() ?? typeof(TState);
            typeString = t.IsPrimitive || t == typeof(string) || t == typeof(decimal) ? "" : t.ToString();
        }
        catch
        {
            typeString = "";
        }

        if (!string.IsNullOrEmpty(text) && !StringComparer.Ordinal.Equals(text, typeString))
        {
            return text;
        }

        try
        {
            return JsonConvert.SerializeObject(state);
        }
        catch
        {
            return text ?? typeString;
        }
    }

    public bool IsEnabled(LogLevel logLevel)
    {
        return logLevel >= _provider.HostLogLevel;
    }

    private sealed class Scope(GCHandle logger, string text) : IDisposable, ILogger
    {
        private GCHandle _logger = logger;
        public Scope? Parent { get; set; }

        public string Text => text;

        public bool Disposed => !_logger.IsAllocated;

        private JsonRpcLogger? GetLogger()
        {
            if (!_logger.IsAllocated) return null;
            if (_logger.Target is JsonRpcLogger l) return l;
            var parent = Parent;
            Dispose();

            while (parent is not null)
            {
                var c = parent.Parent;
                parent.Dispose();
                parent = c;
            }

            return null;
        }

        public void Dispose()
        {
            if (_logger is { IsAllocated: true, Target: JsonRpcLogger l })
            {
                if (ReferenceEquals(l._current, this))
                {
                    l._current = Parent;
                }

                if (ReferenceEquals(l._root, this))
                {
                    l._root = Parent;
                }
            }

            _logger = default;
            Parent = null;
        }


        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            GetLogger()?.Log(this, logLevel, eventId, state, exception, formatter);
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return GetLogger()?.IsEnabled(logLevel) ?? false;
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull
        {
            return GetLogger()?.BeginScope(this, state);
        }
    }
}