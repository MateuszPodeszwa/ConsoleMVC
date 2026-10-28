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

    /// <summary>Logs that a <see cref="TerminalServer"/> is listening.</summary>
    [LoggerMessage(EventId = 5, Level = LogLevel.Information, Message = "Serving remote sessions on {EndPoint} ({Protocol}, {Security})")]
    public static partial void Serving(ILogger logger, System.Net.EndPoint endPoint, TerminalProtocol protocol, string security);

    /// <summary>Logs a client connecting.</summary>
    [LoggerMessage(EventId = 6, Level = LogLevel.Information, Message = "Session {Session} started for {RemoteEndPoint} ({TerminalType}, {Columns}x{Rows})")]
    public static partial void SessionStarted(ILogger logger, string session, System.Net.EndPoint? remoteEndPoint, string? terminalType, int columns, int rows);

    /// <summary>Logs a session ending.</summary>
    [LoggerMessage(EventId = 7, Level = LogLevel.Information, Message = "Session {Session} ended")]
    public static partial void SessionEnded(ILogger logger, string session);

    /// <summary>Logs an exception that ended one session; the server and the other sessions carry on.</summary>
    [LoggerMessage(EventId = 8, Level = LogLevel.Error, Message = "Session {Session} failed")]
    public static partial void SessionFailed(ILogger logger, Exception exception, string session);

    /// <summary>Logs a connection refused: by the filter, the session limit or a failed TLS handshake.</summary>
    [LoggerMessage(EventId = 9, Level = LogLevel.Warning, Message = "Connection from {RemoteEndPoint} refused: {Reason}")]
    public static partial void ConnectionRefused(ILogger logger, System.Net.EndPoint? remoteEndPoint, string reason);

    /// <summary>Logs an unexpected error while setting up a connection; that connection is closed.</summary>
    [LoggerMessage(EventId = 11, Level = LogLevel.Error, Message = "Connection from {RemoteEndPoint} failed while it was set up")]
    public static partial void ConnectionFailed(ILogger logger, Exception exception, System.Net.EndPoint? remoteEndPoint);

    /// <summary>Logs that the server stopped.</summary>
    [LoggerMessage(EventId = 10, Level = LogLevel.Information, Message = "Stopped serving remote sessions")]
    public static partial void StoppedServing(ILogger logger);
}
