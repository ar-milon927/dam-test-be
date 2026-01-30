using Microsoft.Extensions.Logging;

namespace NinjaDAM.Services.Logging
{
    public class FileLogger : ILogger
    {
        private readonly string _categoryName;
        private readonly string _logDirectory;

        public FileLogger(string categoryName, string logDirectory)
        {
            _categoryName = categoryName;
            _logDirectory = logDirectory;
        }

        public IDisposable BeginScope<TState>(TState state) => default!;

        // Capture both Warning and Error 
        public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Error;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state,
                                Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel)) return;

            try
            {
                Directory.CreateDirectory(_logDirectory);
                var date = DateTime.UtcNow.ToString("yyyy-MM-dd");
                var logPath = Path.Combine(_logDirectory, $"{date}.log");

                var logMessage = $"{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} [{logLevel}] {_categoryName} - {formatter(state, exception)}";

                if (exception != null)
                {
                    logMessage += $" | Exception: {exception.Message}";
                }

                File.AppendAllText(logPath, logMessage + Environment.NewLine);
            }
            catch
            {
                // Avoid throwing exceptions from logger 
            }
        }
    }

    public class FileLoggerProvider : ILoggerProvider
    {
        private readonly string _logDirectory;

        public FileLoggerProvider(string logDirectory)
        {
            _logDirectory = logDirectory;
        }

        public ILogger CreateLogger(string categoryName) => new FileLogger(categoryName, _logDirectory);

        public void Dispose() { }
    }
}
