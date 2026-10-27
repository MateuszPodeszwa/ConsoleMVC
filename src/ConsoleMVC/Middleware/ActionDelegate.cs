namespace ConsoleMVC.Mvc;

/// <summary>
/// The next step of the action pipeline: the following middleware or, at the end, the action itself.
/// </summary>
/// <param name="context">The current navigation cycle.</param>
/// <returns>A task that completes when the rest of the pipeline has run.</returns>
/// <example>
/// <code>
/// app.Use(async (context, next) =>
/// {
///     Console.Title = context.Route.ToString();
///     await next(context);
/// });
/// </code>
/// </example>
public delegate Task ActionDelegate(ActionContext context);
