using System.ComponentModel;
using System.Reflection;

namespace ConsoleMVC.Mvc;

/// <summary>
/// Binds form data collected by views to controller action method parameters.
/// Supports simple type parameters (string, int, bool, etc.) matched by name,
/// and complex type parameters whose public properties are populated from form data.
/// </summary>
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

        var args = new object?[parameters.Length];
        formData ??= new Dictionary<string, string>();

        for (var i = 0; i < parameters.Length; i++)
        {
            args[i] = BindParameter(parameters[i], formData);
        }

        return args;
    }

    private static object? BindParameter(ParameterInfo parameter, Dictionary<string, string> formData)
    {
        var paramType = parameter.ParameterType;

        // Try to bind as a simple type directly from form data by parameter name
        if (IsSimpleType(paramType))
        {
            return TryGetAndConvert(formData, parameter.Name!, paramType, parameter.DefaultValue);
        }

        // Complex type — create instance and populate properties from form data
        return BindComplexType(paramType, formData);
    }

    private static object? BindComplexType(Type type, Dictionary<string, string> formData)
    {
        object instance;
        try
        {
            instance = Activator.CreateInstance(type)!;
        }
        catch
        {
            return null;
        }

        var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanWrite);

        foreach (var prop in properties)
        {
            if (!IsSimpleType(prop.PropertyType))
                continue;

            var value = TryGetAndConvert(formData, prop.Name, prop.PropertyType, defaultValue: null);
            if (value is not null)
            {
                prop.SetValue(instance, value);
            }
        }

        return instance;
    }

    private static object? TryGetAndConvert(
        Dictionary<string, string> formData,
        string name,
        Type targetType,
        object? defaultValue)
    {
        // Case-insensitive key lookup
        var key = formData.Keys.FirstOrDefault(k =>
            string.Equals(k, name, StringComparison.OrdinalIgnoreCase));

        if (key is null || !formData.TryGetValue(key, out var stringValue))
        {
            if (defaultValue != DBNull.Value && defaultValue is not null)
                return defaultValue;

            return targetType.IsValueType ? Activator.CreateInstance(targetType) : null;
        }

        return ConvertValue(stringValue, targetType);
    }

    internal static object? ConvertValue(string value, Type targetType)
    {
        // Handle nullable types
        var underlyingType = Nullable.GetUnderlyingType(targetType);
        if (underlyingType is not null)
        {
            if (string.IsNullOrEmpty(value))
                return null;

            targetType = underlyingType;
        }

        if (targetType == typeof(string))
            return value;

        if (string.IsNullOrEmpty(value))
            return targetType.IsValueType ? Activator.CreateInstance(targetType) : null;

        var converter = TypeDescriptor.GetConverter(targetType);
        if (converter.CanConvertFrom(typeof(string)))
        {
            try
            {
                return converter.ConvertFromInvariantString(value);
            }
            catch
            {
                return targetType.IsValueType ? Activator.CreateInstance(targetType) : null;
            }
        }

        return targetType.IsValueType ? Activator.CreateInstance(targetType) : null;
    }

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
               || t == typeof(Guid);
    }
}
