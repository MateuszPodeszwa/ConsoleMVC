using System.ComponentModel;
using System.Reflection;

namespace ConsoleMVC.Mvc;

/// <summary>
/// Binds form data collected by views to controller action method parameters.
/// Supports simple type parameters (string, int, bool, etc.) matched by name,
/// and complex type parameters whose public properties are populated from form data.
/// </summary>
/// <remarks>
/// Key matching is case-insensitive; when two keys differ only by case, the first one wins.
/// Properties of complex models are only assigned when their key is present and the value
/// converts successfully, so property initialisers act as defaults.
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
    public static object?[]? BindParameters(MethodInfo method, Dictionary<string, string>? formData)
    {
        var parameters = method.GetParameters();

        if (parameters.Length == 0)
            return null;

        var values = ToCaseInsensitive(formData);
        var args = new object?[parameters.Length];

        for (var i = 0; i < parameters.Length; i++)
            args[i] = BindParameter(parameters[i], values);

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

    private static object? BindParameter(ParameterInfo parameter, IReadOnlyDictionary<string, string> values)
    {
        var parameterType = parameter.ParameterType;

        if (!IsSimpleType(parameterType))
            return BindComplexType(parameterType, values);

        if (values.TryGetValue(parameter.Name!, out var raw) && TryConvert(raw, parameterType, out var converted))
            return converted;

        // Missing or unconvertible: use the declared default (e.g. int page = 1), else the type default
        return parameter.HasDefaultValue ? parameter.DefaultValue : DefaultOf(parameterType);
    }

    private static object? BindComplexType(Type type, IReadOnlyDictionary<string, string> values)
    {
        object instance;
        try
        {
            instance = Activator.CreateInstance(type)!;
        }
        catch (Exception e) when (e is MissingMethodException or MemberAccessException or ArgumentException or NotSupportedException)
        {
            // No accessible parameterless constructor
            return null;
        }

        var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanWrite && p.GetIndexParameters().Length == 0 && IsSimpleType(p.PropertyType));

        foreach (var property in properties)
        {
            if (values.TryGetValue(property.Name, out var raw) && TryConvert(raw, property.PropertyType, out var converted))
                property.SetValue(instance, converted);
        }

        return instance;
    }

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

    private static object? DefaultOf(Type type) => type.IsValueType ? Activator.CreateInstance(type) : null;
}
