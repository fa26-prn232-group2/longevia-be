using Microsoft.Extensions.Logging;

namespace Worker.UnitTests.Fakes;

/// <summary>
/// In-memory logger capturing rendered lines so tests can assert redaction
/// (no token, email, or body ever reaches the logs).
/// </summary>
public sealed class CaptureLoggerProvider : ILoggerProvider
{
    private readonly List<(LogLevel Level, string Message)> _entries = [];

    /// <summary>Captured (level, rendered message) pairs.</summary>
    public IReadOnlyList<(LogLevel Level, string Message)> Entries => _entries;

    /// <inheritdoc />
    public ILogger CreateLogger(string categoryName) => new CaptureLogger(_entries);

    /// <inheritdoc />
    public void Dispose()
    {
    }

    private sealed class CaptureLogger(List<(LogLevel Level, string Message)> entries) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => NullScope.Instance;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter) =>
            entries.Add((logLevel, formatter(state, exception)));

        private sealed class NullScope : IDisposable
        {
            public static readonly NullScope Instance = new();

            public void Dispose()
            {
            }
        }
    }
}
