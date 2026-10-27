namespace ConsoleMVC.Mvc;

/// <summary>
/// Settings that must be known before the builder reads configuration: the command-line arguments, the environment
/// and the folder <c>appsettings.json</c> is read from. Pass them to <see cref="MvcApplication.CreateBuilder(MvcApplicationOptions)"/>.
/// </summary>
/// <remarks>Mirrors ASP.NET Core's <c>WebApplicationOptions</c>.</remarks>
/// <example>
/// <code>
/// var builder = MvcApplication.CreateBuilder(new MvcApplicationOptions
/// {
///     Args = args,
///     EnvironmentName = "Development",          // reads appsettings.Development.json too
///     ContentRootPath = "/etc/my-console-app"   // where appsettings.json lives
/// });
/// </code>
/// </example>
public sealed class MvcApplicationOptions
{
    /// <summary>
    /// Gets or initialises the command-line arguments. They are added to the configuration last, so
    /// <c>--Shop:Name=Corner</c> overrides <c>appsettings.json</c>.
    /// </summary>
    /// <example><code>new MvcApplicationOptions { Args = args };</code></example>
    public string[] Args { get; init; } = [];

    /// <summary>
    /// Gets or initialises the environment name, e.g. <c>Development</c>. When <see langword="null"/>, the
    /// <c>DOTNET_ENVIRONMENT</c> environment variable is used, and <c>Production</c> when that is not set either.
    /// </summary>
    /// <example><code>new MvcApplicationOptions { EnvironmentName = Environments.Staging };</code></example>
    public string? EnvironmentName { get; init; }

    /// <summary>
    /// Gets or initialises the folder configuration files are read from. When <see langword="null"/>, the folder of
    /// the application (<see cref="AppContext.BaseDirectory"/>), where the build copies <c>appsettings.json</c>.
    /// </summary>
    /// <example><code>new MvcApplicationOptions { ContentRootPath = Directory.GetCurrentDirectory() };</code></example>
    public string? ContentRootPath { get; init; }

    /// <summary>
    /// Gets or initialises the application name. When <see langword="null"/>, the name of the entry assembly.
    /// </summary>
    /// <example><code>new MvcApplicationOptions { ApplicationName = "Inventory" };</code></example>
    public string? ApplicationName { get; init; }
}
