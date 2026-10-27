namespace ConsoleMVC.Mvc;

/// <summary>
/// A class-based middleware registered with <see cref="MvcApplication.UseMiddleware{TMiddleware}"/>. A new instance is
/// created for every navigation cycle, so its constructor may take scoped services.
/// </summary>
/// <example>
/// <code>
/// public sealed class RequireSignInMiddleware(ISession session) : IActionMiddleware
/// {
///     public Task InvokeAsync(ActionContext context, ActionDelegate next)
///     {
///         if (!session.IsSignedIn &amp;&amp; context.Route.Controller != "Account")
///         {
///             context.RedirectTo("SignIn", "Account");
///             return Task.CompletedTask;
///         }
///
///         return next(context);
///     }
/// }
/// </code>
/// </example>
public interface IActionMiddleware
{
    /// <summary>
    /// Runs this middleware. Call <paramref name="next"/> to continue to the action, or set
    /// <see cref="ActionContext.Result"/> instead to short-circuit it.
    /// </summary>
    /// <param name="context">The current navigation cycle.</param>
    /// <param name="next">The rest of the pipeline.</param>
    /// <returns>A task that completes when this middleware has finished.</returns>
    Task InvokeAsync(ActionContext context, ActionDelegate next);
}
