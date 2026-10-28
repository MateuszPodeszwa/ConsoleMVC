using System.Globalization;
using ConsoleMVC.Rendering;

namespace ConsoleMVC.Mvc;

/// <summary>
/// How an application speaks its users' language: <see cref="MvcApplicationBuilder.Localization"/>. Views and
/// controllers use <c>IStringLocalizer&lt;T&gt;</c> (registered by the framework) for their own text; these options cover
/// the culture, the framework's own messages and validation messages.
/// </summary>
/// <example>
/// <code>
/// builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");
/// builder.Localization.Culture = CultureInfo.GetCultureInfo("pl-PL");
/// builder.Localization.FrameworkTextResource = typeof(SharedResource);
/// builder.Localization.LocalizeValidation = true;
/// </code>
/// </example>
public sealed class MvcLocalizationOptions
{
    /// <summary>The setting that chooses <see cref="Culture"/>: <c>--ConsoleMVC:Culture=pl-PL</c> or <c>ConsoleMVC__Culture=pl-PL</c>.</summary>
    public const string CultureSetting = "ConsoleMVC:Culture";

    /// <summary>
    /// Gets or sets the culture of the run, used for formatting (<see cref="CultureInfo.CurrentCulture"/>) and for
    /// resources (<see cref="CultureInfo.CurrentUICulture"/>). <see langword="null"/> (the default) uses the
    /// <see cref="CultureSetting"/> setting, or else the system's culture.
    /// </summary>
    /// <example><code>builder.Localization.Culture = CultureInfo.GetCultureInfo("de-DE");</code></example>
    public CultureInfo? Culture { get; set; }

    /// <summary>Gets or sets the framework's own messages. Defaults to <see cref="FrameworkText.English"/>.</summary>
    /// <example><code>builder.Localization.Text = FrameworkText.English with { MenuPrompt = "Choose: " };</code></example>
    public FrameworkText Text { get; set; } = FrameworkText.English;

    /// <summary>
    /// Gets or sets a resource type whose resources translate <see cref="Text"/>, one resource per
    /// <see cref="FrameworkText"/> property (<c>MenuPrompt</c>, <c>Required</c>, …), looked up in the run's UI culture.
    /// </summary>
    /// <example><code>builder.Localization.FrameworkTextResource = typeof(SharedResource); // Resources/SharedResource.pl.resx</code></example>
    public Type? FrameworkTextResource { get; set; }

    /// <summary>
    /// Gets or sets whether validation messages and display names are translated: an attribute's <c>ErrorMessage</c>
    /// (and a <c>[Display(Name)]</c>) is used as a resource name, and the translation is formatted like the original.
    /// </summary>
    /// <example><code>[Required(ErrorMessage = "NameRequired")] public string Name { get; set; } = "";</code></example>
    public bool LocalizeValidation { get; set; }

    /// <summary>
    /// Gets or sets the resource type for validation messages, or <see langword="null"/> (the default) for resources
    /// named after each model (and after the controller, for simple parameters).
    /// </summary>
    public Type? ValidationResource { get; set; }

    /// <summary>Returns a copy, so the application keeps the options it was built with.</summary>
    internal MvcLocalizationOptions Copy() => (MvcLocalizationOptions)MemberwiseClone();
}
