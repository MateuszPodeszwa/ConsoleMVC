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
    /// Returns the writable properties of <paramref name="type"/> (including inherited and private setters)
    /// marked with <see cref="InjectAttribute"/>.
    /// </summary>
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
