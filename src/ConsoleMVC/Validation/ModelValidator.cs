using System.Diagnostics.CodeAnalysis;
using System.ComponentModel.DataAnnotations;
using System.Reflection;
using ConsoleMVC.Rendering;
using Microsoft.Extensions.Localization;

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
    /// <param name="localizerFor">
    /// Returns the resources that translate the messages of a model type (or of the controller, for simple
    /// parameters); <see langword="null"/> leaves messages as they are.
    /// </param>
    public static void Validate(MethodInfo method, object?[]? arguments, ModelStateDictionary modelState,
        Func<Type, IStringLocalizer>? localizerFor = null)
    {
        if (arguments is null)
            return;

        var parameters = method.GetParameters();
        for (var i = 0; i < parameters.Length; i++)
        {
            if (ModelBinder.IsSimpleType(parameters[i].ParameterType))
                ValidateParameter(parameters[i], arguments[i], modelState, localizerFor?.Invoke(method.DeclaringType!));
            else if (arguments[i] is { } model and not CancellationToken)
                ValidateModel(model, modelState, localizerFor);
        }
    }

    /// <summary>
    /// Validates every property of <paramref name="model"/> and records the failures.
    /// </summary>
    /// <param name="model">The object to validate.</param>
    /// <param name="modelState">Receives the errors; a message already recorded for a field is not added again.</param>
    /// <param name="localizerFor">Returns the resources that translate the model's messages; <see langword="null"/> for none.</param>
    /// <returns><see langword="true"/> when the model has no validation failures.</returns>
    /// <remarks>
    /// When a property could not be converted, only the other properties' attributes are checked:
    /// object-level checks (<see cref="IValidatableObject"/>) would compare the fallback value and report
    /// misleading errors, as ASP.NET Core also avoids.
    /// </remarks>
    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "Model types of actions are kept by the generated route table (DynamicDependency on their properties and constructors).")]
    [UnconditionalSuppressMessage("Trimming", "IL2075", Justification = "Model types of actions are kept by the generated route table (DynamicDependency on their properties and constructors).")]
    public static bool ValidateModel(object model, ModelStateDictionary modelState, Func<Type, IStringLocalizer>? localizerFor = null)
    {
        if (localizerFor is not null)
            return ValidateLocalized(model, modelState, localizerFor(model.GetType()));

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
                var message = result.ErrorMessage ?? FrameworkText.Format(FrameworkText.Current.FieldInvalid, member);
                if (!modelState.ErrorsFor(member).Contains(message))
                    modelState.AddModelError(member, message);
            }
        }

        return valid;
    }

    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "Simple parameters are validated with the attributes on the parameter itself.")]
    private static void ValidateParameter(ParameterInfo parameter, object? value, ModelStateDictionary modelState,
        IStringLocalizer? localizer)
    {
        var name = parameter.Name!;
        if (modelState.HasBindingError(name))
            return; // already reported as not convertible

        var context = new ValidationContext(value ?? new object())
        {
            MemberName = name,
            DisplayName = Translate(localizer, parameter.GetCustomAttribute<DisplayAttribute>()?.GetName() ?? name)
        };

        foreach (var attribute in parameter.GetCustomAttributes<ValidationAttribute>())
        {
            var result = attribute.GetValidationResult(value, context);
            if (result != ValidationResult.Success && result is not null)
                modelState.AddModelError(name, Message(attribute, context.DisplayName, result, localizer));
        }
    }

    /// <summary>
    /// Validates like <see cref="Validator.TryValidateObject(object, ValidationContext, ICollection{ValidationResult}, bool)"/>
    /// (property attributes, then the type's attributes, then <see cref="IValidatableObject"/>), translating each message
    /// and display name with <paramref name="localizer"/>.
    /// </summary>
    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "Model types of actions are kept by the generated route table (DynamicDependency on their properties and constructors).")]
    [UnconditionalSuppressMessage("Trimming", "IL2075", Justification = "Model types of actions are kept by the generated route table (DynamicDependency on their properties and constructors).")]
    private static bool ValidateLocalized(object model, ModelStateDictionary modelState, IStringLocalizer localizer)
    {
        var properties = model.GetType()
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead && p.GetIndexParameters().Length == 0)
            .ToArray();

        var valid = true;
        void Add(string member, string message)
        {
            valid = false;
            if (!modelState.ErrorsFor(member).Contains(message))
                modelState.AddModelError(member, message);
        }

        foreach (var property in properties.Where(p => !modelState.HasBindingError(p.Name)))
        {
            var display = Translate(localizer, property.GetCustomAttribute<DisplayAttribute>()?.GetName() ?? property.Name);
            var context = new ValidationContext(model) { MemberName = property.Name, DisplayName = display };
            var value = property.GetValue(model);

            foreach (var attribute in property.GetCustomAttributes<ValidationAttribute>(inherit: true))
            {
                var result = attribute.GetValidationResult(value, context);
                if (result != ValidationResult.Success && result is not null)
                    Add(property.Name, Message(attribute, display, result, localizer));
            }
        }

        // Object-level checks only run on a model whose properties are valid, as Validator.TryValidateObject does
        if (!valid || properties.Any(p => modelState.HasBindingError(p.Name)))
            return valid;

        var objectContext = new ValidationContext(model);
        foreach (var attribute in model.GetType().GetCustomAttributes<ValidationAttribute>(inherit: true))
        {
            var result = attribute.GetValidationResult(model, objectContext);
            if (result != ValidationResult.Success && result is not null)
            {
                foreach (var member in result.MemberNames.DefaultIfEmpty(""))
                    Add(member, Message(attribute, model.GetType().Name, result, localizer));
            }
        }

        if (valid && model is IValidatableObject validatable)
        {
            foreach (var result in validatable.Validate(objectContext))
            {
                var message = result.ErrorMessage is { } text ? Translate(localizer, text) : FrameworkText.Current.FieldInvalid;
                foreach (var member in result.MemberNames.DefaultIfEmpty(""))
                    Add(member, message);
            }
        }

        return valid;
    }

    /// <summary>
    /// The message for a failed <paramref name="attribute"/>: its <c>ErrorMessage</c> translated and formatted with the
    /// attribute's own arguments, or else the message the attribute produced (itself translated when a resource has it).
    /// </summary>
    private static string Message(ValidationAttribute attribute, string display, ValidationResult result, IStringLocalizer? localizer)
    {
        var produced = result.ErrorMessage ?? FrameworkText.Format(FrameworkText.Current.FieldInvalid, display);
        if (localizer is null)
            return produced;

        if (attribute.ErrorMessageResourceType is null && !string.IsNullOrEmpty(attribute.ErrorMessage))
        {
            var template = localizer[attribute.ErrorMessage];
            if (!template.ResourceNotFound)
                return FrameworkText.Format(template.Value, Arguments(attribute, display));
        }

        return Translate(localizer, produced);
    }

    /// <summary>The arguments the built-in attributes format their messages with: the field, then their limits.</summary>
    private static object?[] Arguments(ValidationAttribute attribute, string display) => attribute switch
    {
        StringLengthAttribute length => [display, length.MaximumLength, length.MinimumLength],
        RangeAttribute range => [display, range.Minimum, range.Maximum],
        MinLengthAttribute min => [display, min.Length],
        MaxLengthAttribute max => [display, max.Length],
        LengthAttribute length => [display, length.MinimumLength, length.MaximumLength],
        RegularExpressionAttribute pattern => [display, pattern.Pattern],
        CompareAttribute compare => [display, compare.OtherPropertyDisplayName ?? compare.OtherProperty],
        _ => [display]
    };

    private static string Translate(IStringLocalizer? localizer, string text)
    {
        if (localizer is null || text.Length == 0)
            return text;

        var translated = localizer[text];
        return translated.ResourceNotFound ? text : translated.Value;
    }

}
