using Microsoft.Extensions.Logging;

namespace ConsoleMVC.Mvc;

/// <summary>
/// The messages the framework logs under the <c>ConsoleMVC.Mvc.MvcApplication</c> category. Generated at build time
/// by the logging source generator, so a disabled level costs one check and no formatting.
/// </summary>
internal static partial class MvcApplicationLog
{
    /// <summary>Logs the first route when the application starts.</summary>
    [LoggerMessage(EventId = 1, Level = LogLevel.Information, Message = "Starting at {Route} ({Environment})")]
    public static partial void Starting(ILogger logger, RouteContext route, string environment);

    /// <summary>Logs each navigation cycle (an action and the view it renders, or a redirect).</summary>
    [LoggerMessage(EventId = 2, Level = LogLevel.Debug, Message = "Running {Route}")]
    public static partial void Running(ILogger logger, RouteContext route);

    /// <summary>Logs an exception that ends the application; it is rethrown afterwards.</summary>
    [LoggerMessage(EventId = 3, Level = LogLevel.Error, Message = "Unhandled exception on {Route}")]
    public static partial void UnhandledException(ILogger logger, Exception exception, RouteContext route);

    /// <summary>Logs that the application finished because a view quit.</summary>
    [LoggerMessage(EventId = 4, Level = LogLevel.Information, Message = "Stopped")]
    public static partial void Stopped(ILogger logger);
}
