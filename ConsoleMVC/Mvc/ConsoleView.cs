namespace ConsoleMVC.Mvc;

/// <summary>
/// Non-generic base class for console views. Used internally by the framework
/// for type-agnostic view resolution.
/// </summary>
public abstract class ConsoleView
{
    public ViewDataDictionary ViewData { get; internal set; } = new();

    internal abstract NavigationResult RenderInternal(object? model);
}

/// <summary>
/// Generic base class for console views. Inherit from this to create typed views
/// that receive a strongly-typed ViewModel.
/// </summary>
/// <typeparam name="TModel">The ViewModel type this view expects.</typeparam>
public abstract class ConsoleView<TModel> : ConsoleView
{
    /// <summary>
    /// Render the view to the console and return a NavigationResult
    /// indicating where to go next.
    /// </summary>
    public abstract NavigationResult Render(TModel model);

    internal override NavigationResult RenderInternal(object? model)
    {
        return Render((TModel)model!);
    }
}
