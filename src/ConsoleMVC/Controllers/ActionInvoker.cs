using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.ExceptionServices;

namespace ConsoleMVC.Mvc;

/// <summary>
/// Invokes action methods and awaits asynchronous ones. Supported return types are <see cref="ActionResult"/> (or a
/// subclass) and <see cref="Task{TResult}"/> / <see cref="ValueTask{TResult}"/> of such a type.
/// </summary>
/// <remarks>The adapter that turns a method's return value into a task is built once per method and cached.</remarks>
/// <example><code>ActionResult? result = await ActionInvoker.InvokeAsync(method, controller, args);</code></example>
internal static class ActionInvoker
{
    private static readonly ConcurrentDictionary<MethodInfo, Func<object?, Task<ActionResult?>>> Adapters = new();

    /// <summary>
    /// Returns <see langword="true"/> when <paramref name="returnType"/> can be returned by an action.
    /// </summary>
    /// <param name="returnType">A method's return type.</param>
    /// <example><code>ActionInvoker.IsActionReturnType(typeof(Task&lt;ViewResult&gt;)); // true</code></example>
    public static bool IsActionReturnType(Type returnType) => ResultTypeOf(returnType) is not null;

    /// <summary>
    /// Invokes <paramref name="method"/> and awaits its result when it is asynchronous. The action's own exception is
    /// rethrown with its original stack trace (never a <see cref="TargetInvocationException"/>).
    /// </summary>
    /// <param name="method">The action method.</param>
    /// <param name="controller">The controller to invoke it on.</param>
    /// <param name="arguments">The bound arguments.</param>
    /// <returns>The action's result.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the method is not a valid action or returns a <see langword="null"/> task.</exception>
    public static Task<ActionResult?> InvokeAsync(MethodInfo method, Controller controller, object?[]? arguments)
    {
        var adapter = Adapters.GetOrAdd(method, CreateAdapter);

        object? value;
        try
        {
            value = method.Invoke(controller, arguments);
        }
        catch (TargetInvocationException e) when (e.InnerException is not null)
        {
            ExceptionDispatchInfo.Capture(e.InnerException).Throw();
            throw; // unreachable
        }

        return adapter(value);
    }

    /// <summary>
    /// Returns the <see cref="ActionResult"/> type an action produces, or <see langword="null"/> when
    /// <paramref name="returnType"/> is not a valid action return type.
    /// </summary>
    private static Type? ResultTypeOf(Type returnType)
    {
        if (typeof(ActionResult).IsAssignableFrom(returnType))
            return returnType;

        if (returnType.IsGenericType)
        {
            var definition = returnType.GetGenericTypeDefinition();
            if (definition == typeof(Task<>) || definition == typeof(ValueTask<>))
            {
                var argument = returnType.GetGenericArguments()[0];
                return typeof(ActionResult).IsAssignableFrom(argument) ? argument : null;
            }
        }

        return null;
    }

    private static Func<object?, Task<ActionResult?>> CreateAdapter(MethodInfo method)
    {
        var returnType = method.ReturnType;
        if (ResultTypeOf(returnType) is null)
            throw new InvalidOperationException($"'{method.DeclaringType?.Name}.{method.Name}' does not return an ActionResult.");

        if (typeof(ActionResult).IsAssignableFrom(returnType))
            return value => Task.FromResult((ActionResult?)value);

        var resultProperty = typeof(Task<>).MakeGenericType(returnType.GetGenericArguments()[0]).GetProperty(nameof(Task<int>.Result))!;

        if (returnType.GetGenericTypeDefinition() == typeof(ValueTask<>))
        {
            var asTask = returnType.GetMethod(nameof(ValueTask<int>.AsTask))!;
            return value => AwaitAsync((Task)asTask.Invoke(value, null)!, resultProperty, method);
        }

        return value => AwaitAsync((Task?)value, resultProperty, method);
    }

    private static async Task<ActionResult?> AwaitAsync(Task? task, PropertyInfo resultProperty, MethodInfo method)
    {
        if (task is null)
            throw new InvalidOperationException($"Action '{method.DeclaringType?.Name}.{method.Name}' returned a null task.");

        await task.ConfigureAwait(false);
        return (ActionResult?)resultProperty.GetValue(task);
    }
}
