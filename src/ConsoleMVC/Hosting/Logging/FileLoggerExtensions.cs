using ConsoleMVC.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Configuration;
using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.Logging;

/// <summary>Adds the ConsoleMVC file logger to <see cref="ILoggingBuilder"/>.</summary>
public static class FileLoggerExtensions
{
    /// <summary>
    /// Writes log entries to a file instead of the console, which shows the application. The settings come from the
    /// <c>Logging:File</c> section of the configuration; <paramref name="path"/>, when given, takes precedence.
    /// </summary>
    /// <param name="builder">The logging builder, e.g. <c>builder.Logging</c>.</param>
    /// <param name="path">The log file, relative to the application's folder; <c>logs/app.log</c> when not set anywhere.</param>
    /// <returns>The logging builder, for chaining.</returns>
    /// <example><code>builder.Logging.AddFile("logs/shop.log");</code></example>
    public static ILoggingBuilder AddFile(this ILoggingBuilder builder, string? path = null) =>
        builder.AddFile(options =>
        {
            if (!string.IsNullOrWhiteSpace(path))
                options.Path = path;
        });

    /// <summary>
    /// Writes log entries to a file, with settings from the <c>Logging:File</c> section and then <paramref name="configure"/>.
    /// </summary>
    /// <param name="builder">The logging builder.</param>
    /// <param name="configure">Changes the options, e.g. the size at which the file rolls over.</param>
    /// <returns>The logging builder, for chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when an argument is <see langword="null"/>.</exception>
    /// <example><code>builder.Logging.AddFile(o => { o.Path = "logs/shop.log"; o.MaxFileSizeBytes = 1_048_576; });</code></example>
    public static ILoggingBuilder AddFile(this ILoggingBuilder builder, Action<FileLoggerOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configure);

        builder.AddConfiguration();
        builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<ILoggerProvider, FileLoggerProvider>(services =>
            new FileLoggerProvider(services.GetRequiredService<IOptions<FileLoggerOptions>>(), services.GetService<IHostEnvironment>())));
        LoggerProviderOptions.RegisterProviderOptions<FileLoggerOptions, FileLoggerProvider>(builder.Services);
        builder.Services.Configure(configure);
        return builder;
    }
}
