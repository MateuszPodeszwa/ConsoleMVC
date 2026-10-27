namespace ConsoleMVC.Mvc;

/// <summary>
/// Composes middleware into a single <see cref="ActionDelegate"/>, outermost first, ending in the action itself.
/// </summary>
/// <example>
/// <code>
/// ActionDelegate pipeline = ActionPipeline.Build(components, terminal: InvokeActionAsync);
/// await pipeline(context);
/// </code>
/// </example>
internal static class ActionPipeline
{
    /// <summary>
    /// Builds the pipeline.
    /// </summary>
    /// <param name="components">The middleware in registration order; each wraps the delegate that follows it.</param>
    /// <param name="terminal">The last step, which runs the action.</param>
    /// <returns>The composed pipeline.</returns>
    public static ActionDelegate Build(IReadOnlyList<Func<ActionDelegate, ActionDelegate>> components, ActionDelegate terminal)
    {
        var pipeline = terminal;
        for (var i = components.Count - 1; i >= 0; i--)
            pipeline = components[i](pipeline);

        return pipeline;
    }
}
