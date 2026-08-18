using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;

namespace AndroidCameraShare.Core
{
    /// <summary>
    /// Неблокирующий файловый logger с ограниченной очередью и ротацией.
    /// </summary>
    public sealed class RotatingFileLoggerProvider : ILoggerProvider
    {
        private readonly string _directory;
        private readonly string _fileName;
        private readonly long _maxBytes;
        private readonly int _maxFiles;
        private readonly LogLevel _minimumLevel;
        private readonly Channel<string> _messages;
        private readonly Task _writerTask;
        private int _disposed;

        public RotatingFileLoggerProvider(
            string directory,
            string fileName = "camera-share.log",
            long maxBytes = 512 * 1024,
            int maxFiles = 3,
            LogLevel minimumLevel = LogLevel.Information)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(directory);
            ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
            if (maxBytes <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxBytes));
            }

            if (maxFiles <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxFiles));
            }

            _directory = directory;
            _fileName = fileName;
            _maxBytes = maxBytes;
            _maxFiles = maxFiles;
            _minimumLevel = minimumLevel;
            _messages = Channel.CreateBounded<string>(
                new BoundedChannelOptions(1024)
                {
                    FullMode = BoundedChannelFullMode.DropWrite,
                    SingleReader = true,
                    SingleWriter = false
                });
            _writerTask = WriteLoopAsync();
        }

        public ILogger CreateLogger(string categoryName)
        {
            return new FileLogger(this, categoryName);
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
            {
                return;
            }

            _messages.Writer.TryComplete();
            try
            {
                _writerTask.GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"CameraShare file logger shutdown failed: {ex.Message}");
            }
        }

        private bool IsEnabled(LogLevel level)
        {
            return level >= _minimumLevel && level != LogLevel.None;
        }

        private void Write(
            string category,
            LogLevel level,
            string message,
            Exception? exception)
        {
            if (Volatile.Read(ref _disposed) != 0 || !IsEnabled(level))
            {
                return;
            }

            string text = exception is null
                ? message
                : $"{message} | {exception.GetType().Name}: {exception.Message}";
            string line =
                $"{DateTimeOffset.UtcNow:O} [{level}] {category}: {LogSanitizer.Sanitize(text)}{Environment.NewLine}";
            _messages.Writer.TryWrite(line);
        }

        private async Task WriteLoopAsync()
        {
            try
            {
                Directory.CreateDirectory(_directory);
                await foreach (string message in _messages.Reader.ReadAllAsync())
                {
                    try
                    {
                        RotateIfNeeded(Encoding.UTF8.GetByteCount(message));
                        string path = Path.Combine(_directory, _fileName);
                        await File.AppendAllTextAsync(path, message, Encoding.UTF8).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"CameraShare file logger write failed: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"CameraShare file logger unavailable: {ex.Message}");
            }
        }

        private void RotateIfNeeded(int nextMessageBytes)
        {
            string path = Path.Combine(_directory, _fileName);
            if (!File.Exists(path)
                || new FileInfo(path).Length + nextMessageBytes <= _maxBytes)
            {
                return;
            }

            for (int index = _maxFiles - 1; index >= 1; index--)
            {
                string target = $"{path}.{index}";
                string source = index == 1 ? path : $"{path}.{index - 1}";
                if (File.Exists(target))
                {
                    File.Delete(target);
                }

                if (File.Exists(source))
                {
                    File.Move(source, target);
                }
            }
        }

        private sealed class FileLogger : ILogger
        {
            private readonly RotatingFileLoggerProvider _provider;
            private readonly string _category;

            public FileLogger(RotatingFileLoggerProvider provider, string category)
            {
                _provider = provider;
                _category = category;
            }

            public IDisposable? BeginScope<TState>(TState state)
                where TState : notnull
            {
                return null;
            }

            public bool IsEnabled(LogLevel logLevel)
            {
                return _provider.IsEnabled(logLevel);
            }

            public void Log<TState>(
                LogLevel logLevel,
                EventId eventId,
                TState state,
                Exception? exception,
                Func<TState, Exception?, string> formatter)
            {
                ArgumentNullException.ThrowIfNull(formatter);
                if (!IsEnabled(logLevel))
                {
                    return;
                }

                _provider.Write(_category, logLevel, formatter(state, exception), exception);
            }
        }
    }

    public static partial class LogSanitizer
    {
        [GeneratedRegex(
            "(?i)(x-pin|pin|cookie)(\\s*[:=]\\s*)([^\\s,;|]+)",
            RegexOptions.CultureInvariant)]
        private static partial Regex PinValueRegex();

        [GeneratedRegex(
            "(?i)(\"sdp\"\\s*:\\s*\")[^\"]*(\")",
            RegexOptions.CultureInvariant)]
        private static partial Regex JsonSdpRegex();

        public static string Sanitize(string message)
        {
            string sanitized = PinValueRegex().Replace(message, "$1$2[redacted]");
            sanitized = JsonSdpRegex().Replace(sanitized, "$1[redacted]$2");
            int rawSdp = sanitized.IndexOf("v=0", StringComparison.Ordinal);
            return rawSdp >= 0
                ? $"{sanitized[..rawSdp]}[SDP redacted]"
                : sanitized;
        }
    }
}
