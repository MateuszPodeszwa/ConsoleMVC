using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Reflection;
using ConsoleMVC.Rendering;

namespace ConsoleMVC.Mvc;

/// <summary>
/// Binds form data collected by views to controller action method parameters.
/// Supports simple type parameters (string, int, bool, etc.) matched by name,
/// and complex type parameters whose public properties are populated from form data.
/// </summary>
/// <remarks>
/// Key matching is case-insensitive; when two keys differ only by case, the first one wins.
/// Properties of complex models are only assigned when their key is present and the value
/// converts successfully, so property initialisers act as defaults. Models without a parameterless constructor,
/// such as positional records, are created through their public constructor, binding its parameters by name.
/// </remarks>
/// <example>
/// <code>
/// var args = ModelBinder.BindParameters(actionMethod, new Dictionary&lt;string, string&gt; { ["age"] = "30" });
/// actionMethod.Invoke(controller, args);
/// </code>
/// </example>
internal static class ModelBinder
{
    /// <summary>
    /// Creates an array of argument values for the given action method by binding
    /// from the provided form data dictionary.
    /// </summary>
    /// <param name="method">The action method whose parameters should be bound.</param>
    /// <param name="formData">
    /// The form data dictionary submitted by the view, or <see langword="null"/> if no data was posted.
    /// </param>
    /// <returns>
    /// An array of bound argument values matching the method's parameter list,
    /// or <see langword="null"/> if the method has no parameters.
    /// </returns>
    public static object?[]? BindParameters(MethodInfo method, Dictionary<string, string>? formData) =>
        BindParameters(method, formData, modelState: null);

    /// <summary>
    /// Binds the action's parameters from form data, recording values that cannot be converted in
    /// <paramref name="modelState"/>.
    /// </summary>
    /// <param name="method">The action method whose parameters should be bound.</param>
    /// <param name="formData">The posted form data, or <see langword="null"/>.</param>
    /// <param name="modelState">Receives conversion errors, or <see langword="null"/> to ignore them.</param>
    /// <returns>The bound arguments, or <see langword="null"/> when the method has no parameters.</returns>
    /// <remarks>
    /// An empty value is not a conversion error: the field simply keeps its default. Use <c>[Required]</c> on a
    /// reference or nullable type (<c>string</c>, <c>int?</c>) to demand a value; a non-nullable value type such as
    /// <c>int</c> always has one (its default), so <c>[Required]</c> cannot fail on it.
    /// </remarks>
    /// <exception cref="InvalidOperationException">
    /// Thrown when a complex parameter has neither a public parameterless constructor nor a public constructor
    /// whose parameters are all simple types.
    /// </exception>
    public static object?[]? BindParameters(MethodInfo method, Dictionary<string, string>? formData, ModelStateDictionary? modelState) =>
        BindParameters(method, formData, routeValues: null, modelState);

    /// <summary>
    /// Binds the action's parameters from form data and route values; form data wins when both have a key.
    /// </summary>
    /// <param name="method">The action method whose parameters should be bound.</param>
    /// <param name="formData">The posted form data, or <see langword="null"/>.</param>
    /// <param name="routeValues">The route values (<c>Orders/Details?id=42</c>), or <see langword="null"/>.</param>
    /// <param name="modelState">Receives conversion errors, or <see langword="null"/> to ignore them.</param>
    /// <returns>The bound arguments, or <see langword="null"/> when the method has no parameters.</returns>
    public static object?[]? BindParameters(MethodInfo method, Dictionary<string, string>? formData,
        IReadOnlyDictionary<string, string>? routeValues, ModelStateDictionary? modelState)
    {
        var parameters = method.GetParameters();

        if (parameters.Length == 0)
            return null;

        var values = ToCaseInsensitive(formData);
        if (routeValues is not null)
        {
            var merged = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var (key, value) in routeValues)
                merged.TryAdd(key, value);
            foreach (var (key, value) in values)
                merged[key] = value; // form data wins

            values = merged;
        }
        var args = new object?[parameters.Length];

        for (var i = 0; i < parameters.Length; i++)
            args[i] = BindParameter(parameters[i], values, modelState);

        return args;
    }

    /// <summary>
    /// Converts a string to <paramref name="targetType"/>, returning the type's default value
    /// (<see langword="null"/>, 0, <see langword="false"/>…) when conversion fails.
    /// </summary>
    /// <param name="value">The string to convert.</param>
    /// <param name="targetType">The destination type.</param>
    /// <returns>The converted value, or the default of <paramref name="targetType"/>.</returns>
    internal static object? ConvertValue(string value, Type targetType) =>
        TryConvert(value, targetType, out var result) ? result : DefaultOf(targetType);

    /// <summary>
    /// Attempts to convert a string to <paramref name="targetType"/> using the invariant culture.
    /// Empty strings convert to <see langword="null"/> for nullable and reference types.
    /// </summary>
    /// <param name="value">The string to convert.</param>
    /// <param name="targetType">The destination type.</param>
    /// <param name="result">The converted value when successful.</param>
    /// <returns><see langword="true"/> when the value was converted.</returns>
    internal static bool TryConvert(string value, Type targetType, out object? result)
    {
        result = null;

        var underlyingType = Nullable.GetUnderlyingType(targetType);
        if (underlyingType is not null)
        {
            if (string.IsNullOrEmpty(value))
                return true; // empty → null for nullable types

            targetType = underlyingType;
        }

        if (targetType == typeof(string))
        {
            result = value;
            return true;
        }

        if (string.IsNullOrEmpty(value))
            return false;

        var converter = TypeDescriptor.GetConverter(targetType);
        if (!converter.CanConvertFrom(typeof(string)))
            return false;

        try
        {
            result = converter.ConvertFromInvariantString(value);
        }
        catch (Exception e) when (e is FormatException or ArgumentException or NotSupportedException or OverflowException)
        {
            return false;
        }

        // EnumConverter accepts any number; only defined values (or flag combinations) are valid input
        if (targetType.IsEnum && result is not null && !IsValidEnumValue(targetType, result))
        {
            result = null;
            return false;
        }

        return true;
    }

    /// <summary>
    /// Converts a form value to <paramref name="targetType"/>: a simple type with <see cref="TryConvert"/>, or a
    /// collection of a simple type (see <see cref="IsSimpleCollection"/>) by splitting the value on commas, as
    /// <c>&lt;select multiple&gt;</c> posts it. Parts are trimmed and empty parts dropped.
    /// </summary>
    /// <param name="value">The form value, e.g. <c>"1,3"</c>.</param>
    /// <param name="targetType">The destination type, e.g. <c>List&lt;int&gt;</c>.</param>
    /// <param name="result">The converted value when successful.</param>
    /// <returns><see langword="true"/> when the value (every part, for a collection) was converted.</returns>
    /// <example><code>ModelBinder.TryConvertField("1, 3", typeof(int[]), out var ids); // new[] { 1, 3 }</code></example>
    internal static bool TryConvertField(string value, Type targetType, out object? result)
    {
        if (!IsSimpleCollection(targetType, out var elementType))
            return TryConvert(value, targetType, out result);

        result = null;
        var parts = value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        var items = Array.CreateInstance(elementType, parts.Length);

        for (var i = 0; i < parts.Length; i++)
        {
            if (!TryConvert(parts[i], elementType, out var item))
                return false;

            items.SetValue(item, i);
        }

        // Arrays are assigned as they are; List<T> also serves every collection interface it implements
        result = targetType.IsArray ? items : Activator.CreateInstance(typeof(List<>).MakeGenericType(elementType), items);
        return true;
    }

    /// <summary>
    /// Returns <see langword="true"/> for an array, <see cref="List{T}"/> or a collection interface that
    /// <see cref="List{T}"/> implements (<see cref="IEnumerable{T}"/>, <see cref="ICollection{T}"/>,
    /// <see cref="IList{T}"/>, <see cref="IReadOnlyCollection{T}"/>, <see cref="IReadOnlyList{T}"/>) of a simple type.
    /// </summary>
    /// <param name="type">The type to check.</param>
    /// <param name="elementType">The element type when the result is <see langword="true"/>.</param>
    /// <example><code>ModelBinder.IsSimpleCollection(typeof(List&lt;string&gt;), out var element); // true, string</code></example>
    internal static bool IsSimpleCollection(Type type, out Type elementType)
    {
        elementType = typeof(object);

        if (type.IsArray)
        {
            elementType = type.GetElementType()!;
            return type.GetArrayRank() == 1 && IsSimpleType(elementType);
        }

        if (!type.IsGenericType)
            return false;

        var definition = type.GetGenericTypeDefinition();
        var supported = definition == typeof(List<>) || definition == typeof(IEnumerable<>) || definition == typeof(ICollection<>)
                        || definition == typeof(IList<>) || definition == typeof(IReadOnlyCollection<>) || definition == typeof(IReadOnlyList<>);

        elementType = type.GetGenericArguments()[0];
        return supported && IsSimpleType(elementType);
    }

    /// <summary>
    /// Returns <see langword="true"/> for defined enum members and, for <see cref="FlagsAttribute"/> enums,
    /// combinations of defined flags.
    /// </summary>
    private static bool IsValidEnumValue(Type enumType, object value)
    {
        if (Enum.IsDefined(enumType, value))
            return true;

        if (!enumType.IsDefined(typeof(FlagsAttribute), inherit: false))
            return false;

        var allFlags = Enum.GetValues(enumType).Cast<object>()
            .Aggregate(0UL, (mask, member) => mask | Convert.ToUInt64(member, System.Globalization.CultureInfo.InvariantCulture));
        var bits = Convert.ToUInt64(value, System.Globalization.CultureInfo.InvariantCulture);

        return (bits & ~allFlags) == 0;
    }

    /// <summary>
    /// Returns <see langword="true"/> for types bound directly from a single form value.
    /// </summary>
    /// <param name="type">The type to check.</param>
    internal static bool IsSimpleType(Type type)
    {
        var t = Nullable.GetUnderlyingType(type) ?? type;

        return t.IsPrimitive
               || t.IsEnum
               || t == typeof(string)
               || t == typeof(decimal)
               || t == typeof(DateTime)
               || t == typeof(DateTimeOffset)
               || t == typeof(TimeSpan)
               || t == typeof(DateOnly)
               || t == typeof(TimeOnly)
               || t == typeof(Half)
               || t == typeof(Int128)
               || t == typeof(UInt128)
               || t == typeof(Guid)
               || t == typeof(Uri)
               || t == typeof(Version);
    }

    private static object? BindParameter(ParameterInfo parameter, IReadOnlyDictionary<string, string> values,
        ModelStateDictionary? modelState, string? displayName = null)
    {
        var parameterType = parameter.ParameterType;

        // Supplied by the application loop (the token passed to RunAsync), never from form data
        if (parameterType == typeof(CancellationToken))
            return CancellationToken.None;

        var isCollection = IsSimpleCollection(parameterType, out _);
        if (!IsSimpleType(parameterType) && !isCollection)
            return BindComplexType(parameterType, values, modelState);

        if (values.TryGetValue(parameter.Name!, out var raw))
        {
            if (TryConvertField(raw, parameterType, out var converted))
                return converted;

            ReportConversionError(modelState, parameter.Name!, displayName ?? DisplayNameOf(parameter), raw);
        }

        // Missing or unconvertible: use the declared default (e.g. int page = 1), else the type default
        return parameter.HasDefaultValue ? parameter.DefaultValue : DefaultOf(parameterType);
    }

    private static object? BindComplexType(Type type, IReadOnlyDictionary<string, string> values, ModelStateDictionary? modelState)
    {
        var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanWrite && p.GetIndexParameters().Length == 0 && (IsSimpleType(p.PropertyType) || IsSimpleCollection(p.PropertyType, out _)))
            .ToArray();

        // Arrays of complex types cannot be bound from form data (one value per key); the parameter receives null
        if (type.IsArray)
            return null;

        var (instance, boundByConstructor) = CreateModel(type, properties, values, modelState);

        foreach (var property in properties.Where(p => !boundByConstructor.Contains(p.Name)))
        {
            if (!values.TryGetValue(property.Name, out var raw))
                continue;

            if (TryConvertField(raw, property.PropertyType, out var converted))
                property.SetValue(instance, converted);
            else
                ReportConversionError(modelState, property.Name, DisplayNameOf(property), raw);
        }

        return instance;
    }

    /// <summary>
    /// Creates a model with its public parameterless constructor or, failing that (positional records), with the
    /// public constructor that has the most parameters, all of simple types, bound from <paramref name="values"/>.
    /// </summary>
    /// <returns>The model and the names of the properties its constructor already received.</returns>
    /// <exception cref="InvalidOperationException">Thrown when no suitable constructor exists.</exception>
    private static (object Instance, HashSet<string> BoundByConstructor) CreateModel(Type type, PropertyInfo[] properties,
        IReadOnlyDictionary<string, string> values, ModelStateDictionary? modelState)
    {
        var bound = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (type.IsValueType || type.GetConstructor(Type.EmptyTypes) is not null)
            return (Activator.CreateInstance(type)!, bound);

        var constructor = type.IsAbstract
            ? null
            : type.GetConstructors()
                .Where(c => c.GetParameters().All(p => p.Name is not null && (IsSimpleType(p.ParameterType) || IsSimpleCollection(p.ParameterType, out _))))
                .OrderByDescending(c => c.GetParameters().Length)
                .FirstOrDefault();

        if (constructor is null)
        {
            throw new InvalidOperationException(
                $"Cannot bind form data to '{type.FullName}': it needs a public parameterless constructor, or a public " +
                "constructor whose parameters are all simple types or lists of them (such as a positional record with simple properties).");
        }

        var parameters = constructor.GetParameters();
        var args = new object?[parameters.Length];
        for (var i = 0; i < parameters.Length; i++)
        {
            // A record's [property: Display(Name = ...)] sits on the property, not the parameter
            var property = properties.FirstOrDefault(p => string.Equals(p.Name, parameters[i].Name, StringComparison.OrdinalIgnoreCase));
            args[i] = BindParameter(parameters[i], values, modelState, property is null ? null : DisplayNameOf(property));
            bound.Add(parameters[i].Name!);
        }

        return (constructor.Invoke(args), bound);
    }

    /// <summary>Returns the <c>[Display(Name = ...)]</c> of a parameter, or its name.</summary>
    private static string DisplayNameOf(ParameterInfo parameter) =>
        parameter.GetCustomAttribute<DisplayAttribute>()?.GetName() ?? parameter.Name!;

    /// <summary>Returns the <c>[Display(Name = ...)]</c> of a property, or its name.</summary>
    private static string DisplayNameOf(PropertyInfo property) =>
        property.GetCustomAttribute<DisplayAttribute>()?.GetName() ?? property.Name;

    /// <summary>
    /// Copies form data into a case-insensitive dictionary. When keys differ only by case, the first is kept.
    /// </summary>
    private static IReadOnlyDictionary<string, string> ToCaseInsensitive(Dictionary<string, string>? formData)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (formData is null)
            return values;

        foreach (var (key, value) in formData)
            values.TryAdd(key, value);

        return values;
    }

    /// <summary>
    /// Records that <paramref name="raw"/> could not be converted, unless it is empty. The error is keyed by the field
    /// name and worded with its display name.
    /// </summary>
    private static void ReportConversionError(ModelStateDictionary? modelState, string key, string displayName, string raw)
    {
        if (modelState is null || string.IsNullOrWhiteSpace(raw))
            return;

        modelState.AddBindingError(key, FrameworkText.Format(FrameworkText.Current.InvalidValue, raw, displayName));
    }

    private static object? DefaultOf(Type type) => type.IsValueType ? Activator.CreateInstance(type) : null;
}
