using System.Runtime.CompilerServices;

namespace ScribanLanguage.Extensions;

public static class LoggerExtensions
{
    public static LoggerScope Function(this ILogger? logger, [CallerMemberName] string memberName = "")
    {
        return Scope(logger, memberName);
    }

    public static LoggerScope Scope<TState>(this ILogger? logger, TState state) where TState : notnull
    {
        var scope = logger?.BeginScope(state);
        return new(scope as ILogger ?? logger, scope);
    }
 
    public static LoggerWithLevel? Level(this ILogger? logger, LogLevel logLevel)
    {
        return logger?.IsEnabled(logLevel) ?? false ? new(logger, logLevel) : null;
    }

    public static LoggerWithLevel? Trace(this ILogger? logger)
    {
        return Level(logger, LogLevel.Trace);
    }

    public static LoggerWithLevel? Debug(this ILogger? logger)
    {
        return Level(logger, LogLevel.Debug);
    }

    public static LoggerWithLevel? Info(this ILogger? logger)
    {
        return Level(logger, LogLevel.Information);
    }

    public static LoggerWithLevel? Warn(this ILogger? logger)
    {
        return Level(logger, LogLevel.Warning);
    }

    public static LoggerWithLevel? Error(this ILogger? logger)
    {
        return Level(logger, LogLevel.Error);
    }

    public static LoggerWithLevel? Critical(this ILogger? logger)
    {
        return Level(logger, LogLevel.Critical);
    }

    public sealed class LoggerScope(ILogger? logger, IDisposable? resource) : ILogger, IDisposable
    {
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            logger?.Log(logLevel, eventId, state, exception, formatter);
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return logger?.IsEnabled(logLevel) ?? false;
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull
        {
            return logger?.BeginScope(state);
        }

        public void Dispose()
        {
            resource?.Dispose();
        }
    }

    public readonly struct LoggerWithLevel(ILogger? logger, LogLevel currentLogLevel)
    {
        public void Log<TState>(EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            logger?.Log(currentLogLevel, eventId, state, exception, formatter);
        }

        public void Log(EventId eventId, Exception? exception,
            [StructuredMessageTemplate] string message, params object?[]? args)
        {
            logger?.Log(currentLogLevel, eventId, exception, message, args ?? []);
        }

        public void Log(Exception? exception, [StructuredMessageTemplate] string message,
            params object?[]? args)
        {
            logger?.Log(currentLogLevel, exception, message, args ?? []);
        }

        public void Log([StructuredMessageTemplate] string message, params object?[]? args)
        {
            logger?.Log(currentLogLevel, message, args ?? []);
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return logger?.IsEnabled(logLevel) ?? false;
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull
        {
            return logger?.BeginScope(state);
        }
    }
}