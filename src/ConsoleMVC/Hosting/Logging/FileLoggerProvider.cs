using System.Globalization;
using System.Text;
using System.Threading.Channels;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ConsoleMVC.Mvc;

/// <summary>
/// Writes log entries to a file, one line per entry: <c>2026-10-27T21:04:05.123Z INFO  Category: message</c>.
/// Entries are queued and written on a background task, so logging never makes a screen wait for the disk. The file
/// rolls over by size (see <see cref="FileLoggerOptions"/>). Continuation lines of a message or an exception are
/// indented, so text inside a message cannot pass itself off as a separate entry.
/// </summary>
/// <remarks>Its alias is <c>File</c>: levels can be set in the <c>Logging:File:LogLevel</c> section.</remarks>
/// <example>
/// <code>
/// builder.Logging.AddFile("logs/shop.log");
///
/// // without the builder:
/// using var provider = new FileLoggerProvider(new FileLoggerOptions { Path = "/var/log/shop.log" });
/// provider.CreateLogger("Shop").LogWarning("Stock is low");
/// </code>
/// </example>
[ProviderAlias("File")]
public sealed class FileLoggerProvider : ILoggerProvider
{
    private const string Indent = "    ";

    private readonly FileLoggerOptions _options;
    private readonly string _path;
    private readonly TimeProvider _clock;
    private readonly Channel<string> _queue;
    private readonly Task _writer;
    private long _dropped;
    private int _disposed;

    /// <summary>
    /// Creates the provider used by <see cref="FileLoggerExtensions.AddFile(ILoggingBuilder, string?)"/>: the options come from
    /// the configuration and the callback, and a relative path is resolved against the content root.
    /// </summary>
    /// <param name="options">The options.</param>
    /// <param name="environment">The environment whose content root relative paths start from, or <see langword="null"/>.</param>
    /// <example><code>services.AddSingleton&lt;ILoggerProvider, FileLoggerProvider&gt;();</code></example>
    public FileLoggerProvider(IOptions<FileLoggerOptions> options, IHostEnvironment? environment = null)
        : this(options.Value, environment?.ContentRootPath ?? AppContext.BaseDirectory, TimeProvider.System)
    {
    }

    /// <summary>
    /// Creates a provider with explicit options; a relative path is resolved against the application's folder.
    /// </summary>
    /// <param name="options">The options.</param>
    /// <param name="baseDirectory">The folder relative paths start from; the application's folder when <see langword="null"/>.</param>
    /// <param name="clock">The clock for timestamps; the system clock when <see langword="null"/>.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="options"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when the path is empty or the limits are negative.</exception>
    /// <example><code>using var provider = new FileLoggerProvider(new FileLoggerOptions { Path = "app.log" });</code></example>
    public FileLoggerProvider(FileLoggerOptions options, string? baseDirectory = null, TimeProvider? clock = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.Path, nameof(options));
        if (options.MaxFileSizeBytes < 0 || options.RetainedFileCount < 0 || options.QueueLength < 1)
            throw new ArgumentException("MaxFileSizeBytes and RetainedFileCount cannot be negative, and QueueLength must be at least 1.", nameof(options));

        _options = options;
        _path = System.IO.Path.GetFullPath(options.Path, baseDirectory ?? AppContext.BaseDirectory);
        _clock = clock ?? TimeProvider.System;
        _queue = Channel.CreateBounded<string>(new BoundedChannelOptions(options.QueueLength)
        {
            SingleReader = true,
            FullMode = BoundedChannelFullMode.DropWrite
        }, _ => Interlocked.Increment(ref _dropped));
        _writer = Task.Run(WriteAsync);
    }

    /// <summary>Gets the full path of the log file.</summary>
    public string FilePath => _path;

    /// <inheritdoc />
    public ILogger CreateLogger(string categoryName) => new FileLogger(this, categoryName);

    /// <summary>Writes the entries still queued and closes the file. Waits up to five seconds.</summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 1)
            return;

        _queue.Writer.TryComplete();
        _writer.Wait(TimeSpan.FromSeconds(5));
    }

    /// <summary>Formats an entry and queues it; never blocks.</summary>
    private void Enqueue(LogLevel level, string category, string message, Exception? exception)
    {
        var line = new StringBuilder(64 + message.Length);
        line.Append(_clock.GetUtcNow().ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture))
            .Append(' ').Append(LevelName(level)).Append(' ').Append(category).Append(": ");
        AppendIndented(line, message);

        if (exception is not null)
            AppendIndented(line.Append('\n').Append(Indent), exception.ToString());

        _queue.Writer.TryWrite(line.ToString());
    }

    /// <summary>Appends text, indenting its continuation lines and dropping escape sequences.</summary>
    private static void AppendIndented(StringBuilder line, string text)
    {
        foreach (var c in text)
        {
            if (c == '\n')
                line.Append('\n').Append(Indent);
            else if (c == '\r' || (char.IsControl(c) && c != '\t'))
                continue;
            else
                line.Append(c);
        }
    }

    private async Task WriteAsync()
    {
        StreamWriter? writer = null;
        long size = 0; // bytes in the current file, counted as they are written (the writer buffers)
        try
        {
            while (await _queue.Reader.WaitToReadAsync().ConfigureAwait(false))
            {
                while (_queue.Reader.TryRead(out var entry))
                {
                    if (writer is null)
                    {
                        writer = Open();
                        size = writer.BaseStream.Length;
                    }

                    var lost = Interlocked.Exchange(ref _dropped, 0);
                    if (lost > 0)
                        size += await WriteLineAsync(writer, string.Create(CultureInfo.InvariantCulture,
                            $"{_clock.GetUtcNow():yyyy-MM-ddTHH:mm:ss.fffZ} WARN  ConsoleMVC.FileLogger: {lost} entries were dropped because the disk was too slow.")).ConfigureAwait(false);

                    size += await WriteLineAsync(writer, entry).ConfigureAwait(false);

                    if (_options.MaxFileSizeBytes > 0 && size >= _options.MaxFileSizeBytes)
                    {
                        await writer.DisposeAsync().ConfigureAwait(false);
                        writer = null;
                        Roll();
                    }
                }

                // Nothing else waiting: make what was written visible
                if (writer is not null)
                    await writer.FlushAsync().ConfigureAwait(false);
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // The log cannot be written (disk full, no permission): the application keeps running without it
        }
        finally
        {
            if (writer is not null)
                await writer.DisposeAsync().ConfigureAwait(false);
        }
    }

    /// <summary>Writes one entry and returns its size in bytes.</summary>
    private static async Task<long> WriteLineAsync(StreamWriter writer, string line)
    {
        await writer.WriteLineAsync(line).ConfigureAwait(false);
        return writer.Encoding.GetByteCount(line) + 1;
    }

    private StreamWriter Open()
    {
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(_path)!);
        var stream = new FileStream(_path, FileMode.Append, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete);
        return new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)) { NewLine = "\n" };
    }

    /// <summary>app.log → app.1.log → app.2.log …, deleting the oldest beyond <see cref="FileLoggerOptions.RetainedFileCount"/>.</summary>
    private void Roll()
    {
        var oldest = RolledPath(_options.RetainedFileCount);
        if (_options.RetainedFileCount == 0)
        {
            File.Delete(_path);
            return;
        }

        File.Delete(oldest);
        for (var i = _options.RetainedFileCount - 1; i >= 1; i--)
        {
            var from = RolledPath(i);
            if (File.Exists(from))
                File.Move(from, RolledPath(i + 1));
        }

        File.Move(_path, RolledPath(1));
    }

    private string RolledPath(int index) =>
        System.IO.Path.Combine(System.IO.Path.GetDirectoryName(_path)!,
            $"{System.IO.Path.GetFileNameWithoutExtension(_path)}.{index}{System.IO.Path.GetExtension(_path)}");

    private static string LevelName(LogLevel level) => level switch
    {
        LogLevel.Trace => "TRACE",
        LogLevel.Debug => "DEBUG",
        LogLevel.Information => "INFO ",
        LogLevel.Warning => "WARN ",
        LogLevel.Error => "ERROR",
        LogLevel.Critical => "CRIT ",
        _ => "NONE "
    };

    /// <summary>The logger for one category; filtering by level is done by the logging framework.</summary>
    private sealed class FileLogger(FileLoggerProvider provider, string category) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None && Volatile.Read(ref provider._disposed) == 0;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
                return;

            provider.Enqueue(logLevel, category, formatter(state, exception), exception);
        }
    }
}
