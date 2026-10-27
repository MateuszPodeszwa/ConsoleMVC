using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ConsoleMVC.Mvc;

/// <summary>The <see cref="ILoggingBuilder"/> behind <see cref="MvcApplicationBuilder.Logging"/>.</summary>
/// <param name="services">The application's services, where providers and filters are registered.</param>
internal sealed class MvcLoggingBuilder(IServiceCollection services) : ILoggingBuilder
{
    /// <inheritdoc />
    public IServiceCollection Services { get; } = services;
}
