using System.ComponentModel.DataAnnotations;
using System.Reflection;

namespace ConsoleMVC.Mvc;

/// <summary>
/// Validates bound action arguments with DataAnnotations attributes and records failures in a
/// <see cref="ModelStateDictionary"/>.
/// </summary>
/// <remarks>
/// <list type="bullet">
///   <item><description>Simple parameters are checked against the attributes placed on the parameter itself.</description></item>
///   <item><description>Complex parameters are checked with <see cref="Validator.TryValidateObject(object, ValidationContext, ICollection{ValidationResult}, bool)"/>,
///   including <see cref="IValidatableObject"/>.</description></item>
///   <item><description>Fields that already have an error (for example a value that could not be converted) are not
///   validated again, so the user sees one clear message per field.</description></item>
/// </list>
/// See <c>docs/design/form-validation.md</c> §4.
/// </remarks>
/// <example><code>ModelValidator.Validate(actionMethod, arguments, controller.ModelState);</code></example>
internal static class ModelValidator
{
    /// <summary>
    /// Validates the arguments bound for <paramref name="method"/>.
    /// </summary>
    /// <param name="method">The action method.</param>
    /// <param name="arguments">The bound arguments, or <see langword="null"/> for a parameterless action.</param>
    /// <param name="modelState">Receives the errors.</param>
    public static void Validate(MethodInfo method, object?[]? arguments, ModelStateDictionary modelState)
    {
        if (arguments is null)
            return;

        var parameters = method.GetParameters();
        for (var i = 0; i < parameters.Length; i++)
        {
            if (ModelBinder.IsSimpleType(parameters[i].ParameterType))
                ValidateParameter(parameters[i], arguments[i], modelState);
            else if (arguments[i] is { } model and not CancellationToken)
                ValidateModel(model, modelState);
        }
    }

    /// <summary>
    /// Validates every property of <paramref name="model"/> and records the failures.
    /// </summary>
    /// <param name="model">The object to validate.</param>
    /// <param name="modelState">Receives the errors; a message already recorded for a field is not added again.</param>
    /// <returns><see langword="true"/> when the model has no validation failures.</returns>
    /// <remarks>
    /// When a property could not be converted, only the other properties' attributes are checked:
    /// object-level checks (<see cref="IValidatableObject"/>) would compare the fallback value and report
    /// misleading errors, as ASP.NET Core also avoids.
    /// </remarks>
    public static bool ValidateModel(object model, ModelStateDictionary modelState)
    {
        var properties = model.GetType()
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead && p.GetIndexParameters().Length == 0)
            .ToArray();

        var results = new List<ValidationResult>();
        if (properties.Any(p => modelState.HasBindingError(p.Name)))
        {
            foreach (var property in properties.Where(p => !modelState.HasBindingError(p.Name)))
            {
                var context = new ValidationContext(model) { MemberName = property.Name };
                Validator.TryValidateProperty(property.GetValue(model), context, results);
            }
        }
        else
        {
            Validator.TryValidateObject(model, new ValidationContext(model), results, validateAllProperties: true);
        }

        var valid = true;
        foreach (var result in results)
        {
            foreach (var member in result.MemberNames.DefaultIfEmpty(""))
            {
                if (modelState.HasBindingError(member))
                    continue;

                valid = false;
                var message = result.ErrorMessage ?? $"The {member} field is invalid.";
                if (!modelState.ErrorsFor(member).Contains(message))
                    modelState.AddModelError(member, message);
            }
        }

        return valid;
    }

    private static void ValidateParameter(ParameterInfo parameter, object? value, ModelStateDictionary modelState)
    {
        var name = parameter.Name!;
        if (modelState.HasBindingError(name))
            return; // already reported as not convertible

        var context = new ValidationContext(value ?? new object())
        {
            MemberName = name,
            DisplayName = parameter.GetCustomAttribute<DisplayAttribute>()?.GetName() ?? name
        };

        foreach (var attribute in parameter.GetCustomAttributes<ValidationAttribute>())
        {
            var result = attribute.GetValidationResult(value, context);
            if (result != ValidationResult.Success && result is not null)
                modelState.AddModelError(name, result.ErrorMessage ?? $"The {context.DisplayName} field is invalid.");
        }
    }

}
