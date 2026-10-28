namespace ConsoleMVC.Mvc;

/// <summary>
/// Writes type names as C# does, for messages a developer copies into code: <c>IOptions&lt;GreetingOptions&gt;</c>
/// rather than the runtime's <c>IOptions`1</c>.
/// </summary>
internal static class TypeNames
{
    /// <summary>
    /// The type's C# name, with generic arguments and, when <paramref name="qualified"/>, namespaces.
    /// </summary>
    /// <param name="type">The type.</param>
    /// <param name="qualified">Whether to include the namespaces.</param>
    /// <returns>The name, e.g. <c>IOptions&lt;GreetingOptions&gt;</c>.</returns>
    /// <example><code>TypeNames.Of(typeof(List&lt;int&gt;)); // "List&lt;Int32&gt;"</code></example>
    public static string Of(Type type, bool qualified = false)
    {
        if (type.IsArray)
            return Of(type.GetElementType()!, qualified) + "[]";

        var name = qualified && type.Namespace is { } space ? $"{space}.{type.Name}" : type.Name;
        if (!type.IsGenericType)
            return name;

        var tick = name.IndexOf('`');
        var arguments = string.Join(", ", type.GetGenericArguments().Select(argument => Of(argument, qualified)));
        return $"{(tick < 0 ? name : name[..tick])}<{arguments}>";
    }
}
