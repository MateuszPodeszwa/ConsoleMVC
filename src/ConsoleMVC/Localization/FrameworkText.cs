using System.Reflection;
using Microsoft.Extensions.Localization;

namespace ConsoleMVC.Rendering;

/// <summary>
/// Every message and prompt the framework shows by itself: menu prompts, field errors, the pager's status line and
/// binding errors. Replace it with <c>builder.Localization.Text</c>, or translate it from resources with
/// <c>builder.Localization.FrameworkTextResource</c>. Messages with <c>{0}</c> are composite format strings.
/// </summary>
/// <remarks>
/// The text in use is <see cref="Current"/>, which the application sets for the length of a run. It flows with the
/// run's asynchronous context, so two runs in one process (for example two remote sessions) can use different languages.
/// </remarks>
/// <example>
/// <code>
/// builder.Localization.Text = FrameworkText.English with { MenuPrompt = "Wybierz opcję: ", Required = "To pole jest wymagane." };
/// </code>
/// </example>
public sealed record FrameworkText
{
    private static readonly AsyncLocal<FrameworkText?> InUse = new();

    /// <summary>Gets the built-in English text.</summary>
    public static FrameworkText English { get; } = new();

    /// <summary>
    /// Gets the text in use: the one set for the current run, or <see cref="English"/> outside a run.
    /// </summary>
    /// <example><code>var prompt = FrameworkText.Current.MenuPrompt;</code></example>
    public static FrameworkText Current => InUse.Value ?? English;

    /// <summary>The prompt of a <c>&lt;menu&gt;</c>.</summary>
    public string MenuPrompt { get; init; } = MenuElement.DefaultPrompt;

    /// <summary>Shown when a menu answer is not one of the keys.</summary>
    public string MenuInvalidChoice { get; init; } = MenuElement.DefaultErrorMessage;

    /// <summary>Shown when a required field is left empty.</summary>
    public string Required { get; init; } = InputElement.RequiredMessage;

    /// <summary>Shown when a required <c>&lt;checkbox&gt;</c> is not ticked.</summary>
    public string CheckboxRequired { get; init; } = CheckboxElement.RequiredMessage;

    /// <summary>Shown when a typed <c>&lt;checkbox&gt;</c> answer is neither yes nor no.</summary>
    public string CheckboxAnswer { get; init; } = CheckboxElement.AnswerMessage;

    /// <summary>The letter for yes in a <c>&lt;checkbox&gt;</c> prompt (<c>[y/N]</c>); it is also accepted as an answer.</summary>
    public string YesLetter { get; init; } = "y";

    /// <summary>The letter for no in a <c>&lt;checkbox&gt;</c> prompt; it is also accepted as an answer.</summary>
    public string NoLetter { get; init; } = "n";

    /// <summary>The word for yes, accepted as a <c>&lt;checkbox&gt;</c> answer (as are <c>y</c>, <c>yes</c>, <c>true</c> and <c>1</c>).</summary>
    public string YesWord { get; init; } = "yes";

    /// <summary>The word for no, accepted as a <c>&lt;checkbox&gt;</c> answer (as are <c>n</c>, <c>no</c>, <c>false</c> and <c>0</c>).</summary>
    public string NoWord { get; init; } = "no";

    /// <summary>Shown when a <c>&lt;number&gt;</c> answer is not a number.</summary>
    public string NotANumber { get; init; } = NumberElement.NotANumberMessage;

    /// <summary>Shown when a <c>&lt;number decimals="0"&gt;</c> answer has decimals.</summary>
    public string WholeNumber { get; init; } = NumberElement.WholeNumberMessage;

    /// <summary>Shown when a number may have one decimal and has more.</summary>
    public string DecimalPlace { get; init; } = "Enter a number with at most 1 decimal place.";

    /// <summary>Shown when a number has too many decimals: <c>{0}</c> is how many are allowed (two or more).</summary>
    public string DecimalPlaces { get; init; } = "Enter a number with at most {0} decimal places.";

    /// <summary>Shown when a number is out of range: <c>{0}</c> and <c>{1}</c> are the limits.</summary>
    public string NumberRange { get; init; } = "Enter a number from {0} to {1}.";

    /// <summary>Shown when a number is below its minimum, <c>{0}</c>.</summary>
    public string NumberAtLeast { get; init; } = "Enter a number of at least {0}.";

    /// <summary>Shown when a number is above its maximum, <c>{0}</c>.</summary>
    public string NumberAtMost { get; init; } = "Enter a number of at most {0}.";

    /// <summary>Shown when a <c>&lt;date&gt;</c> answer is not a date.</summary>
    public string NotADate { get; init; } = DateElement.NotADateMessage;

    /// <summary>Shown when a date is out of range: <c>{0}</c> and <c>{1}</c> are the limits.</summary>
    public string DateRange { get; init; } = "Enter a date from {0} to {1}.";

    /// <summary>Shown when a date is before its minimum, <c>{0}</c>.</summary>
    public string DateOnOrAfter { get; init; } = "Enter a date on or after {0}.";

    /// <summary>Shown when a date is after its maximum, <c>{0}</c>.</summary>
    public string DateOnOrBefore { get; init; } = "Enter a date on or before {0}.";

    /// <summary>Shown when a typed choice is not one of them: <c>{0}</c> is the answer.</summary>
    public string UnknownChoice { get; init; } = ChoiceFieldElement.UnknownChoiceMessage;

    /// <summary>Shown when a filter matches no choice: <c>{0}</c> is the filter.</summary>
    public string NoMatch { get; init; } = ChoiceFieldElement.NoMatchMessage;

    /// <summary>Shown when a typed name matches several choices: <c>{0}</c> is the answer, <c>{1}</c> the matches.</summary>
    public string Ambiguous { get; init; } = ChoiceFieldElement.AmbiguousMessage;

    /// <summary>The prompt of a typed <c>&lt;select&gt;</c> or <c>&lt;radio&gt;</c>: <c>{0}</c> is the range, e.g. <c>1-3</c>.</summary>
    public string ChooseOne { get; init; } = "Choose {0}";

    /// <summary>The prompt of a typed <c>&lt;select multiple&gt;</c>: <c>{0}</c> is the range, <c>{1}</c> an example.</summary>
    public string ChooseSeveral { get; init; } = "Choose one or more of {0}, e.g. {1}";

    /// <summary>The status line of <c>&lt;scroll&gt;</c>: <c>{0}</c> and <c>{1}</c> are the lines shown, <c>{2}</c> the total.</summary>
    public string ScrollStatus { get; init; } = "Lines {0}-{1} of {2}  ↑↓ PgUp PgDn Home End  Enter continues";

    /// <summary>A binding error: <c>{0}</c> is the value typed, <c>{1}</c> the field.</summary>
    public string InvalidValue { get; init; } = "The value '{0}' is not valid for {1}.";

    /// <summary>A validation error without a message of its own: <c>{0}</c> is the field.</summary>
    public string FieldInvalid { get; init; } = "The {0} field is invalid.";

    /// <summary>
    /// Returns a copy of <paramref name="fallback"/> (or <see cref="English"/>) with every message that
    /// <paramref name="localizer"/> has a resource for, named after the property: <c>MenuPrompt</c>, <c>Required</c>, ….
    /// Resources are looked up in the current UI culture.
    /// </summary>
    /// <param name="localizer">The resources, for example <c>IStringLocalizer&lt;SharedResource&gt;</c>.</param>
    /// <param name="fallback">The text for messages without a resource.</param>
    /// <returns>The translated text.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="localizer"/> is <see langword="null"/>.</exception>
    /// <example><code>var text = FrameworkText.FromLocalizer(app.Services.GetRequiredService&lt;IStringLocalizer&lt;SharedResource&gt;&gt;());</code></example>
    public static FrameworkText FromLocalizer(IStringLocalizer localizer, FrameworkText? fallback = null)
    {
        ArgumentNullException.ThrowIfNull(localizer);

        var text = (fallback ?? English) with { };
        foreach (var property in typeof(FrameworkText).GetProperties(BindingFlags.Public | BindingFlags.Instance)
                     .Where(p => p.PropertyType == typeof(string) && p.CanWrite))
        {
            var resource = localizer[property.Name];
            if (!resource.ResourceNotFound && !string.IsNullOrEmpty(resource.Value))
                property.SetValue(text, resource.Value);
        }

        return text;
    }

    /// <summary>Uses <paramref name="text"/> as <see cref="Current"/> until the returned scope is disposed.</summary>
    internal static IDisposable Use(FrameworkText text)
    {
        var previous = InUse.Value;
        InUse.Value = text;
        return new Scope(previous);
    }

    /// <summary>Formats a message in the current culture, keeping the message as it is when its placeholders are wrong.</summary>
    internal static string Format(string message, params object?[] arguments)
    {
        try
        {
            return string.Format(System.Globalization.CultureInfo.CurrentCulture, message, arguments);
        }
        catch (FormatException)
        {
            return message;
        }
    }

    private sealed class Scope(FrameworkText? previous) : IDisposable
    {
        public void Dispose() => InUse.Value = previous;
    }
}
