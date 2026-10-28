using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace ConsoleMVC.Mvc;

/// <summary>
/// The controllers, actions and views of an assembly, written out at build time by the ConsoleMVC source generator so the
/// router needs no reflection to find, create and call them: start-up is faster, and trimmed and Native AOT applications
/// keep everything they use. The generator registers it with <see cref="RouteTableAttribute"/>; assemblies without one
/// are scanned with reflection as before.
/// </summary>
/// <example>
/// <code>
/// // Generated (simplified):
/// internal sealed class RouteTable : IRouteTable
/// {
///     public IReadOnlyList&lt;ControllerRoute&gt; Controllers { get; } =
///     [
///         new("Home", typeof(HomeController), services => new HomeController(),
///             [new("Index", typeof(HomeController).GetMethod("Index", Type.EmptyTypes)!, (c, a) => ActionRoute.FromResult(((HomeController)c).Index()))])
///     ];
///     public IReadOnlyList&lt;ViewRoute&gt; Views { get; } = [new("Home/Index", typeof(IndexView), () => new IndexView())];
/// }
/// </code>
/// </example>
public interface IRouteTable
{
    /// <summary>Gets the controllers, with their route names and actions.</summary>
    IReadOnlyList<ControllerRoute> Controllers { get; }

    /// <summary>Gets the views, keyed <c>Controller/Action</c> (<c>Area/Controller/Action</c> in an area).</summary>
    IReadOnlyList<ViewRoute> Views { get; }
}

/// <summary>A controller in an <see cref="IRouteTable"/>.</summary>
/// <param name="Name">The route name, e.g. <c>Home</c> or <c>Admin/Users</c>.</param>
/// <param name="Type">The controller type.</param>
/// <param name="Create">Creates the controller with its constructor's services, or <see langword="null"/> to create it with <see cref="Microsoft.Extensions.DependencyInjection.ActivatorUtilities"/>.</param>
/// <param name="Actions">The actions.</param>
/// <example><code>new ControllerRoute("Home", typeof(HomeController), services => new HomeController(), actions);</code></example>
public sealed record ControllerRoute(
    string Name,
    [property: DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicMethods)]
    [param: DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicMethods)]
    Type Type,
    Func<IServiceProvider, Controller>? Create,
    IReadOnlyList<ActionRoute> Actions);

/// <summary>An action in an <see cref="IRouteTable"/>.</summary>
/// <param name="Name">The action's name.</param>
/// <param name="Method">The action method, whose parameters are bound and validated.</param>
/// <param name="Invoke">Calls the action with the bound arguments and returns its result.</param>
/// <example><code>new ActionRoute("Index", method, (c, a) => ActionRoute.FromResult(((HomeController)c).Index()));</code></example>
public sealed record ActionRoute(string Name, MethodInfo Method, Func<Controller, object?[]?, Task<ActionResult?>> Invoke)
{
    /// <summary>Returns the result of a synchronous action.</summary>
    /// <param name="result">The result.</param>
    /// <returns>A completed task.</returns>
    /// <example><code>ActionRoute.FromResult(controller.Index());</code></example>
    public static Task<ActionResult?> FromResult(ActionResult? result) => Task.FromResult(result);

    /// <summary>Awaits the result of an asynchronous action.</summary>
    /// <typeparam name="TResult">The action's result type.</typeparam>
    /// <param name="task">The action's task.</param>
    /// <returns>The result.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the action returned a <see langword="null"/> task.</exception>
    /// <example><code>ActionRoute.FromTask(controller.LoadAsync());</code></example>
    public static async Task<ActionResult?> FromTask<TResult>(Task<TResult>? task) where TResult : ActionResult? =>
        await (task ?? throw new InvalidOperationException("The action returned a null task.")).ConfigureAwait(false);

    /// <summary>Awaits the result of an action that returns a <see cref="ValueTask{TResult}"/>.</summary>
    /// <typeparam name="TResult">The action's result type.</typeparam>
    /// <param name="task">The action's task.</param>
    /// <returns>The result.</returns>
    /// <example><code>ActionRoute.FromValueTask(controller.LoadAsync());</code></example>
    public static async Task<ActionResult?> FromValueTask<TResult>(ValueTask<TResult> task) where TResult : ActionResult? =>
        await task.ConfigureAwait(false);
}

/// <summary>A view in an <see cref="IRouteTable"/>.</summary>
/// <param name="Key">The view's key: <c>Controller/Action</c>, or <c>Area/Controller/Action</c>.</param>
/// <param name="Type">The view type.</param>
/// <param name="Create">Creates the view.</param>
/// <example><code>new ViewRoute("Home/Index", typeof(IndexView), () => new IndexView());</code></example>
public sealed record ViewRoute(
    string Key,
    [property: DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.NonPublicProperties)]
    [param: DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.NonPublicProperties)]
    Type Type,
    Func<ConsoleView> Create);

/// <summary>
/// Names the <see cref="IRouteTable"/> of an assembly; the source generator adds it to every assembly with controllers
/// or views.
/// </summary>
/// <param name="tableType">The table type, with a public parameterless constructor.</param>
/// <example><code>[assembly: RouteTable(typeof(ConsoleMVC.Generated.RouteTable))]</code></example>
[AttributeUsage(AttributeTargets.Assembly)]
public sealed class RouteTableAttribute(
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)] Type tableType) : Attribute
{
    /// <summary>Gets the table type.</summary>
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)]
    public Type TableType { get; } = tableType;
}

/// <summary>
/// Registers a view generated from a <c>.cvw</c> file with its assembly's route table; the source generator adds one to
/// every view it compiles, so a view with errors is never listed.
/// </summary>
/// <param name="key">The view's key: <c>Controller/Action</c>, or <c>Area/Controller/Action</c>.</param>
/// <param name="viewType">The view type, with a public parameterless constructor.</param>
/// <example><code>[assembly: GeneratedView("Home/Index", typeof(MyApp.Views.Home.IndexView))]</code></example>
[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
public sealed class GeneratedViewAttribute(
    string key,
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)] Type viewType) : Attribute
{
    /// <summary>Gets the view's key.</summary>
    public string Key { get; } = key;

    /// <summary>Gets the view type.</summary>
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)]
    public Type ViewType { get; } = viewType;
}
