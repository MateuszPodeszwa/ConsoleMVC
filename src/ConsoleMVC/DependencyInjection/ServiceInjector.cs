using System.Diagnostics.CodeAnalysis;
using System.Collections.Concurrent;
using System.Reflection;

namespace ConsoleMVC.Mvc;

/// <summary>
/// Sets the <see cref="InjectAttribute"/> properties of an object from an <see cref="IServiceProvider"/>.
/// The properties of each type are found once and cached.
/// </summary>
/// <example><code>ServiceInjector.InjectProperties(view, scope.ServiceProvider);</code></example>
internal static class ServiceInjector
{
    /// <summary>The injectable properties of every type seen so far.</summary>
    private static readonly ConcurrentDictionary<Type, PropertyInfo[]> PropertiesByType = new();

    /// <summary>
    /// Fills every <see cref="InjectAttribute"/> property of <paramref name="instance"/>.
    /// </summary>
    /// <param name="instance">The object to fill, typically a view.</param>
    /// <param name="services">The services to resolve from.</param>
    /// <exception cref="InvalidOperationException">Thrown when a required service is not registered.</exception>
    public static void InjectProperties(object instance, IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(instance);
        ArgumentNullException.ThrowIfNull(services);

        // Generated views assign their @inject properties themselves
        if (instance is ConsoleViewBase view && view.TryInjectServices(services))
            return;

        var type = instance.GetType();
        foreach (var property in PropertiesByType.GetOrAdd(type, FindInjectableProperties))
        {
            var service = services.GetService(property.PropertyType)
                          ?? throw new InvalidOperationException(
                              $"'{type.Name}' injects '{property.PropertyType.Name} {property.Name}', but no service of type " +
                              $"'{property.PropertyType.FullName}' is registered. Register it with builder.Services, " +
                              $"e.g. builder.Services.AddSingleton<{property.PropertyType.Name}, ...>();");

            property.SetValue(instance, service);
        }
    }

    /// <summary>
    /// Forgets the cached properties of <paramref name="types"/>, or of every type when <see langword="null"/>. Hot reload
    /// calls this, because an edit can add an <c>@inject</c> property to an existing view class.
    /// </summary>
    /// <param name="types">The changed types, or <see langword="null"/> for all.</param>
    public static void ClearCache(Type[]? types)
    {
        if (types is null)
        {
            PropertiesByType.Clear();
            return;
        }

        foreach (var type in types)
            PropertiesByType.TryRemove(type, out _);
    }

    /// <summary>Returns whether the properties of <paramref name="type"/> are cached (for tests).</summary>
    internal static bool IsCached(Type type) => PropertiesByType.ContainsKey(type);

    /// <summary>
    /// Returns the writable properties of <paramref name="type"/> (including inherited and private setters)
    /// marked with <see cref="InjectAttribute"/>.
    /// </summary>
    [UnconditionalSuppressMessage("Trimming", "IL2070", Justification = "Generated views fill their @inject properties themselves (InjectServices); only hand-written [Inject] properties are found by reflection.")]
    [UnconditionalSuppressMessage("Trimming", "IL2075", Justification = "Generated views fill their @inject properties themselves (InjectServices); only hand-written [Inject] properties are found by reflection.")]
    private static PropertyInfo[] FindInjectableProperties(Type type)
    {
        var properties = new List<PropertyInfo>();
        for (var current = type; current is not null && current != typeof(object); current = current.BaseType)
        {
            properties.AddRange(current
                .GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
                .Where(p => p.IsDefined(typeof(InjectAttribute), inherit: true) && p.SetMethod is not null));
        }

        return properties.ToArray();
    }
}
