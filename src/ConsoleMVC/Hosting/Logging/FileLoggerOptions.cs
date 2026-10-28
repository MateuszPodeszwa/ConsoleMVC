namespace ConsoleMVC.Mvc;

/// <summary>
/// Settings of the file logger added by <see cref="Microsoft.Extensions.Logging.FileLoggerExtensions.AddFile(Microsoft.Extensions.Logging.ILoggingBuilder, string?)"/>.
/// They can also come from the <c>Logging:File</c> section of the configuration.
/// </summary>
/// <example>
/// <code>
/// // appsettings.json
/// { "Logging": { "File": { "Path": "logs/shop.log", "MaxFileSizeBytes": 1048576, "RetainedFileCount": 3,
///                          "LogLevel": { "Default": "Debug" } } } }
/// </code>
/// </example>
public sealed class FileLoggerOptions
{
    /// <summary>The default value of <see cref="Path"/>.</summary>
    public const string DefaultPath = "logs/app.log";

    /// <summary>
    /// Gets or sets the log file. A relative path is relative to the application's content root (its folder).
    /// </summary>
    public string Path { get; set; } = DefaultPath;

    /// <summary>
    /// Gets or sets the size at which the file is rolled over: <c>app.log</c> becomes <c>app.1.log</c>, the previous
    /// <c>app.1.log</c> becomes <c>app.2.log</c>, and so on. 0 never rolls over. The default is 10 MB.
    /// </summary>
    public long MaxFileSizeBytes { get; set; } = 10 * 1024 * 1024;

    /// <summary>Gets or sets how many rolled-over files are kept besides the current one. The default is 5.</summary>
    public int RetainedFileCount { get; set; } = 5;

    /// <summary>
    /// Gets or sets how many entries may wait to be written. When the disk cannot keep up, newer entries are dropped
    /// rather than slowing the application down; the next written entry says how many were lost.
    /// </summary>
    public int QueueLength { get; set; } = 10_000;
}
