# Localisation

ConsoleMVC uses the standard .NET localisation: `.resx` resource files read through `IStringLocalizer`, and the
culture of the run for formatting.

## Choosing the culture

```csharp
builder.Localization.Culture = CultureInfo.GetCultureInfo("pl-PL");
```

Or let users choose, with the `ConsoleMVC:Culture` setting:

```bash
dotnet run -- --ConsoleMVC:Culture=pl-PL
export ConsoleMVC__Culture=pl-PL
```

The culture is used for the whole run, for formatting (`@Model.Total` shows `1 234,50` in Polish) and for resources.
Without one, the system's culture is used. An unknown culture name stops the application with a clear message.

## Your own text

`IStringLocalizer<T>` is registered for you. Put the resources in a folder and say where:

```csharp
builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");
```

```
MyApp/
├── SharedResource.cs                 (an empty class that names the resources)
└── Resources/
    ├── SharedResource.resx           (English)
    └── SharedResource.pl.resx        (Polish)
```

```
@using Microsoft.Extensions.Localization
@inject IStringLocalizer<MyApp.SharedResource> L

<box title="@L["GreetingTitle"]">
    <text>@L["GreetingIntro"]</text>
</box>
<input name="Name" label="@L["NameLabel"]" required />
```

Controllers take `IStringLocalizer<T>` in their constructor in the same way.

## The framework's messages

The prompts and messages the framework shows itself (`Select an option: `, `This field is required.`, the `<scroll>`
status line, binding errors, …) are the properties of `FrameworkText` (namespace `ConsoleMVC.Rendering`). Translate them in your resources, one resource
per property name:

```csharp
builder.Localization.FrameworkTextResource = typeof(SharedResource);
```

| Resource | English |
|---|---|
| `MenuPrompt` | `Select an option: ` |
| `MenuInvalidChoice` | `Invalid choice, please try again.` |
| `Required` | `This field is required.` |
| `CheckboxRequired`, `CheckboxAnswer` | `This box must be ticked.`, `Please answer y or n.` |
| `YesLetter`, `NoLetter`, `YesWord`, `NoWord` | `y`, `n`, `yes`, `no` (shown in `[y/N]` and accepted as answers) |
| `NotANumber`, `WholeNumber`, `DecimalPlace`, `DecimalPlaces` | number field messages (`{0}`: decimals allowed) |
| `NumberRange`, `NumberAtLeast`, `NumberAtMost` | `{0}` and `{1}`: the limits |
| `NotADate`, `DateRange`, `DateOnOrAfter`, `DateOnOrBefore` | date field messages |
| `UnknownChoice`, `NoMatch`, `Ambiguous` | choice field messages (`{0}`: the answer) |
| `ChooseOne`, `ChooseSeveral` | typed choice prompts (`{0}`: the range) |
| `ScrollStatus` | `Lines {0}-{1} of {2}  …` |
| `InvalidValue` | `The value '{0}' is not valid for {1}.` |
| `FieldInvalid` | `The {0} field is invalid.` |

Messages without a resource stay in English. You can also set them in code:

```csharp
builder.Localization.Text = FrameworkText.English with { MenuPrompt = "Choose: " };
```

## Validation messages

```csharp
builder.Localization.LocalizeValidation = true;
builder.Localization.ValidationResource = typeof(SharedResource); // or leave it out: resources named after each model
```

An attribute's `ErrorMessage` is then the name of a resource, and `[Display(Name = "…")]` is translated too. The
translation is formatted like the original, so placeholders keep their meaning:

```csharp
[Required(ErrorMessage = "NameRequired")]                       // NameRequired: "Podaj {0}."
[StringLength(30, ErrorMessage = "NameTooLong")]                // NameTooLong: "{0} może mieć najwyżej {1} znaków."
[Display(Name = "Name")]                                        // Name: "Imię"
public string Name { get; set; } = "";
```

| Attribute | `{1}` | `{2}` |
|---|---|---|
| `[StringLength]` | maximum length | minimum length |
| `[Range]` | minimum | maximum |
| `[MinLength]`, `[MaxLength]` | the length | |
| `[Length]` | minimum length | maximum length |
| `[RegularExpression]` | the pattern | |
| `[Compare]` | the other property | |

`{0}` is always the field's (translated) display name. A message without `ErrorMessage` is translated as a whole when
your resources have it. The template does all of this in Polish: try `dotnet run -- --ConsoleMVC:Culture=pl-PL`.
