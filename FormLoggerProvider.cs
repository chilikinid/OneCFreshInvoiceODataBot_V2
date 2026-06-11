using Microsoft.Extensions.Logging;

public sealed class FormLoggerProvider(Action<string> writeLine) : ILoggerProvider
{
    public ILogger CreateLogger(string categoryName)
    {
        return new FormLogger(categoryName, writeLine);
    }

    public void Dispose()
    {
    }

    private sealed class FormLogger(string categoryName, Action<string> writeLine) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull
        {
            return null;
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return logLevel >= LogLevel.Information;
        }

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
                return;

            var message = formatter(state, exception);
            var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} {logLevel,-11} {categoryName}: {message}";
            writeLine(line);

            if (exception is not null)
                writeLine(exception.ToString());
        }
    }
}
