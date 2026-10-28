using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Reflection;

namespace ConsoleMVC.Mvc;

/// <summary>
/// Values passed to an action in the navigation itself, like a query string: <c>Orders/Details?id=42</c>. They are bound
/// to the action's parameters (form data wins when both have a key) but, unlike a posted form, do not trigger validation.
/// </summary>
/// <example>
/// <code>
/// return NavigationResult.To("Orders/Details?id=42");
/// return RedirectToAction("Details", new { id = order.Id });
/// &lt;option key="1" nav="Orders/Details?id=@order.Id"&gt;Open&lt;/option&gt;
/// </code>
/// </example>
public static class RouteValues
{
    /// <summary>
    /// Converts an object to route values: a dictionary is copied, any other object contributes its public properties
    /// (an anonymous object such as <c>new { id = 42 }</c>). Values are formatted with the invariant culture, so they
    /// bind back exactly; <see langword="null"/> values are left out.
    /// </summary>
    /// <param name="values">The values, or <see langword="null"/> for none.</param>
    /// <returns>A new case-insensitive dictionary.</returns>
    /// <example><code>RouteValues.From(new { id = 42, when = DateOnly.FromDateTime(DateTime.Today) });</code></example>
[RequiresUnreferencedCode("Reads the public properties of the object's run-time type, which trimming may remove. Pass the values with their own type (an anonymous object or a class), or a dictionary.")]
    public static Dictionary<string, string> From(object? values) => FromCore(values, values?.GetType());

    /// <summary>
    /// Converts route values to a dictionary, reading the public properties of <typeparamref name="TValues"/>. Calls with an
    /// anonymous object or a class pick this overload, and trimming and Native AOT keep those properties.
    /// </summary>
    /// <typeparam name="TValues">The values' type: a dictionary, or a type whose public properties are the values.</typeparam>
    /// <param name="values">The values, or <see langword="null"/> for none.</param>
    /// <returns>A new case-insensitive dictionary.</returns>
    /// <example><code>RouteValues.From(new { id = 42 }); // { "id": "42" }</code></example>
    public static Dictionary<string, string> From<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] TValues>(TValues values) =>
        FromCore(values, typeof(TValues));

    /// <summary>Copies a dictionary, or reads the public properties of <paramref name="propertySource"/> from <paramref name="values"/>.</summary>
    private static Dictionary<string, string> FromCore(object? values,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] Type? propertySource)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        switch (values)
        {
            case null:
                break;

            case IEnumerable<KeyValuePair<string, string>> strings:
                foreach (var (key, value) in strings)
                    result[key] = value;
                break;

            case IEnumerable<KeyValuePair<string, object?>> objects:
                foreach (var (key, value) in objects)
                    Add(result, key, value);
                break;

            default:
                foreach (var property in (propertySource ?? typeof(object)).GetProperties(BindingFlags.Public | BindingFlags.Instance))
                {
                    if (property.GetIndexParameters().Length == 0)
                        Add(result, property.Name, property.GetValue(values));
                }

                break;
        }

        return result;
    }

    /// <summary>
    /// Splits a navigation target into its controller, action and route values: <c>Area/Controller/Action?…</c> (the
    /// controller is then <c>Area/Controller</c>), <c>Controller/Action?key=value&amp;…</c> or <c>Action?…</c>. Keys and
    /// values are URL-decoded; a key without <c>=</c> gets an empty value.
    /// </summary>
    /// <param name="target">The target, e.g. <c>"Orders/Details?id=42"</c>.</param>
    /// <returns>The controller (<see langword="null"/> for the current one), the action, and the values (<see langword="null"/> when there are none).</returns>
    /// <exception cref="ArgumentException">Thrown when the target is not <c>Area/Controller/Action</c>, <c>Controller/Action</c> or <c>Action</c>.</exception>
    /// <example><code>var (controller, action, values) = RouteValues.ParseTarget("Orders/Details?id=42");</code></example>
    public static (string? Controller, string Action, Dictionary<string, string>? Values) ParseTarget(string target)
    {
        ArgumentNullException.ThrowIfNull(target);

        var query = target.IndexOf('?');
        var path = (query < 0 ? target : target[..query]).Trim();

        // A leading '/' names the application's controller from inside an area: '/Home/Index'
        var root = path.StartsWith('/');
        var parts = (root ? path[1..] : path).Split('/');
        if (parts.Length > 3 || (root && parts.Length < 2) || parts.Any(p => p.Trim().Length == 0))
            throw new ArgumentException($"'{target}' is not a valid navigation target. Use 'Area/Controller/Action', 'Controller/Action' or 'Action', optionally followed by '?key=value'; '/Controller/Action' names the application's controller from inside an area.", nameof(target));

        Dictionary<string, string>? values = null;
        if (query >= 0)
        {
            foreach (var pair in target[(query + 1)..].Split('&', StringSplitOptions.RemoveEmptyEntries))
            {
                var equals = pair.IndexOf('=');
                var key = Uri.UnescapeDataString(equals < 0 ? pair : pair[..equals]).Trim();
                if (key.Length == 0)
                    continue;

                values ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                values[key] = equals < 0 ? "" : Uri.UnescapeDataString(pair[(equals + 1)..]);
            }
        }

        var prefix = root ? "/" : "";
        return parts.Length switch
        {
            3 => (prefix + parts[0].Trim() + "/" + parts[1].Trim(), parts[2].Trim(), values),
            2 => (prefix + parts[0].Trim(), parts[1].Trim(), values),
            _ => (null, parts[0].Trim(), values)
        };
    }

    private static void Add(Dictionary<string, string> result, string key, object? value)
    {
        var text = value switch
        {
            null => null,
            bool flag => flag ? "true" : "false",
            DateTime dateTime => dateTime.ToString("O", CultureInfo.InvariantCulture),
            DateTimeOffset dateTimeOffset => dateTimeOffset.ToString("O", CultureInfo.InvariantCulture),
            DateOnly date => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            TimeOnly time => time.ToString("HH:mm:ss.FFFFFFF", CultureInfo.InvariantCulture),
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
            _ => value.ToString()
        };

        if (text is not null)
            result[key] = text;
    }
}
