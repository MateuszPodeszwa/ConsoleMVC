using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace ConsoleMVC.Mvc;

/// <summary>
/// The environment a ConsoleMVC application runs in: its name (<c>Development</c>, <c>Production</c>…), the folder
/// configuration files are read from and the application name. Available as <see cref="MvcApplicationBuilder.Environment"/>
/// and as an <see cref="IHostEnvironment"/> service, so libraries written for the .NET generic host work unchanged.
/// </summary>
/// <example>
/// <code>
/// if (builder.Environment.IsDevelopment())
///     builder.Logging.SetMinimumLevel(LogLevel.Debug);
/// </code>
/// </example>
public sealed class ConsoleHostEnvironment : IHostEnvironment
{
    /// <summary>
    /// Initialises the environment.
    /// </summary>
    /// <param name="environmentName">The environment name, e.g. <c>Production</c>.</param>
    /// <param name="applicationName">The application name.</param>
    /// <param name="contentRootPath">The folder configuration files are read from.</param>
    /// <exception cref="ArgumentException">Thrown when an argument is empty.</exception>
    /// <example><code>var environment = new ConsoleHostEnvironment("Development", "Shop", AppContext.BaseDirectory);</code></example>
    public ConsoleHostEnvironment(string environmentName, string applicationName, string contentRootPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(environmentName);
        ArgumentException.ThrowIfNullOrWhiteSpace(applicationName);
        ArgumentException.ThrowIfNullOrWhiteSpace(contentRootPath);

        EnvironmentName = environmentName;
        ApplicationName = applicationName;
        ContentRootPath = contentRootPath;
        ContentRootFileProvider = Directory.Exists(contentRootPath)
            ? new PhysicalFileProvider(contentRootPath)
            : new NullFileProvider();
    }

    /// <inheritdoc />
    public string EnvironmentName { get; set; }

    /// <inheritdoc />
    public string ApplicationName { get; set; }

    /// <inheritdoc />
    public string ContentRootPath { get; set; }

    /// <inheritdoc />
    public IFileProvider ContentRootFileProvider { get; set; }
}
