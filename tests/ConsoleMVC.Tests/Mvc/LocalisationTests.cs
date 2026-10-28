using System.ComponentModel.DataAnnotations;
using System.Globalization;
using ConsoleMVC.Mvc;
using ConsoleMVC.Rendering;
using ConsoleMVC.Testing;
using ConsoleMVC.Tests.Mvc.KioskApp;
using ConsoleMVC.Tests.Mvc.KioskApp.Controllers;
using ConsoleMVC.Tests.Mvc.TestApp;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;

namespace ConsoleMVC.Tests.Mvc;

/// <summary>Tests for localisation: culture, framework text and validation messages (docs/design/localisation.md).</summary>
[Collection(TestAppCollection.Name)]
public class LocalisationTests
{
    /// <summary>Resources kept in a dictionary per UI culture name ("" for any).</summary>
    private sealed class DictionaryLocalizer(Dictionary<string, Dictionary<string, string>> cultures) : IStringLocalizer
    {
        public LocalizedString this[string name]
        {
            get
            {
                var table = cultures.GetValueOrDefault(CultureInfo.CurrentUICulture.Name) ?? cultures.GetValueOrDefault("") ?? [];
                return table.TryGetValue(name, out var value)
                    ? new LocalizedString(name, value)
                    : new LocalizedString(name, name, resourceNotFound: true);
            }
        }

        public LocalizedString this[string name, params object[] arguments] => this[name];

        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => [];
    }

    private sealed class DictionaryFactory(IStringLocalizer localizer) : IStringLocalizerFactory
    {
        public List<Type> Created { get; } = [];

        public IStringLocalizer Create(Type resourceSource)
        {
            Created.Add(resourceSource);
            return localizer;
        }

        public IStringLocalizer Create(string baseName, string location) => localizer;
    }

    private sealed class SharedResource;

    private static readonly DictionaryLocalizer Polish = new(new()
    {
        ["pl-PL"] = new()
        {
            ["MenuPrompt"] = "Wybierz opcję: ",
            ["Required"] = "To pole jest wymagane.",
            ["NameRequired"] = "Podaj {0}.",
            ["NameTooLong"] = "{0}: najwyżej {1} znaków.",
            ["Name"] = "Imię",
            ["The Age field is required."] = "Wiek jest wymagany."
        }
    });

    private static MvcTestHost Host(Action<MvcApplicationBuilder> localise) =>
        MvcTestHost.For<KioskController>(builder =>
        {
            builder.DefaultController = "Kiosk";
            builder.Services.AddSingleton<IKioskGreeter, KioskGreeter>();
            localise(builder);
        });

    // ------------------------------------------------------------------ framework text

    [Fact]
    public void FrameworkText_FromLocalizer_TranslatesWhatItHas()
    {
        using var culture = new CultureScope("pl-PL");

        var text = FrameworkText.FromLocalizer(Polish);

        Assert.Equal("Wybierz opcję: ", text.MenuPrompt);
        Assert.Equal("To pole jest wymagane.", text.Required);
        Assert.Equal(FrameworkText.English.NotANumber, text.NotANumber);
    }

    [Fact]
    public void Text_ReplacesTheFrameworksMessages_ForTheRun()
    {
        var run = Host(builder => builder.Localization.Text = FrameworkText.English with { MenuPrompt = "Choose: " }).Run("q");

        Assert.Contains("Choose: ", run.Screens[0]);
        Assert.DoesNotContain(MenuElement.DefaultPrompt, run.Screens[0]);
        Assert.Same(FrameworkText.English, FrameworkText.Current); // back to English once the run is over
    }

    [Fact]
    public void FrameworkTextResource_IsTranslatedInTheRunsCulture()
    {
        var factory = new DictionaryFactory(Polish);
        var run = Host(builder =>
        {
            builder.Services.AddSingleton<IStringLocalizerFactory>(factory);
            builder.Localization.Culture = CultureInfo.GetCultureInfo("pl-PL");
            builder.Localization.FrameworkTextResource = typeof(SharedResource);
        }).Run("q");

        Assert.Contains("Wybierz opcję: ", run.Screens[0]);
        Assert.Contains(typeof(SharedResource), factory.Created);
    }

    [Fact]
    public void StringLocalizers_AreRegistered_ForViewsAndControllers()
    {
        using var app = MvcApplication.CreateBuilder([]).Build();

        Assert.NotNull(app.Services.GetService<IStringLocalizer<LocalisationTests>>());
    }

    // ------------------------------------------------------------------ culture

    [Theory]
    [InlineData(null, "de-DE", "de-DE")]
    [InlineData("fr-FR", null, "fr-FR")]
    [InlineData("fr-FR", "de-DE", "fr-FR")]
    public void Culture_ComesFromCode_OrTheSetting_ForTheRunOnly(string? code, string? setting, string expected)
    {
        var before = CultureInfo.CurrentCulture.Name;
        var seen = new List<string>();
        var host = Host(builder =>
        {
            if (code is not null)
                builder.Localization.Culture = CultureInfo.GetCultureInfo(code);
            if (setting is not null)
                builder.Configuration[MvcLocalizationOptions.CultureSetting] = setting;
        });
        host.ConfigureApplication = app => app.Use(async (context, next) =>
        {
            seen.Add(CultureInfo.CurrentCulture.Name + "|" + CultureInfo.CurrentUICulture.Name);
            await next(context);
        });

        host.Run("q");

        Assert.Equal($"{expected}|{expected}", seen[0]);
        Assert.Equal(before, CultureInfo.CurrentCulture.Name);
    }

    [Fact]
    public void Culture_Setting_MustBeAKnownCulture()
    {
        var host = Host(builder => builder.Configuration[MvcLocalizationOptions.CultureSetting] = "xx-NOPE");

        var error = Assert.Throws<InvalidOperationException>(() => host.Run("q"));

        Assert.Contains("xx-NOPE", error.Message);
    }

    // ------------------------------------------------------------------ validation

    private sealed class Person
    {
        [Required(ErrorMessage = "NameRequired")]
        [StringLength(5, ErrorMessage = "NameTooLong")]
        [Display(Name = "Name")]
        public string? Name { get; set; }

        [Required]
        public int? Age { get; set; }

        [Range(1, 9, ErrorMessage = "Untranslated {0} {1}-{2}")]
        public int Rating { get; set; } = 5;
    }

    [Fact]
    public void Validation_TranslatesMessagesAndDisplayNames_AndFormatsThemLikeTheOriginal()
    {
        using var culture = new CultureScope("pl-PL");
        var state = new ModelStateDictionary();

        ModelValidator.ValidateModel(new Person(), state, _ => Polish);
        Assert.Equal(["Podaj Imię."], state.ErrorsFor("Name"));
        Assert.Equal(["Wiek jest wymagany."], state.ErrorsFor("Age")); // a default message, translated as a whole

        var tooLong = new ModelStateDictionary();
        ModelValidator.ValidateModel(new Person { Name = "Bartholomew", Age = 3, Rating = 0 }, tooLong, _ => Polish);
        Assert.Equal(["Imię: najwyżej 5 znaków."], tooLong.ErrorsFor("Name"));
        Assert.Equal(["Untranslated Rating 1-9"], tooLong.ErrorsFor("Rating"));
    }

    [Fact]
    public void Validation_IsUnchanged_WithoutALocalizer()
    {
        var state = new ModelStateDictionary();

        ModelValidator.ValidateModel(new Person(), state);

        Assert.Equal(["NameRequired"], state.ErrorsFor("Name"));
    }

    [Fact]
    public void BindingErrors_UseTheFrameworkText()
    {
        using var text = FrameworkText.Use(FrameworkText.English with { InvalidValue = "'{0}' nie pasuje do {1}." });
        var state = new ModelStateDictionary();
        var method = typeof(LocalisationTests).GetMethod(nameof(TakesAnAge), System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;

        ModelBinder.BindParameters(method, new Dictionary<string, string> { ["age"] = "abc" }, null, state);

        Assert.Equal(["'abc' nie pasuje do age."], state.ErrorsFor("age"));
    }

    private static void TakesAnAge(int age) { }

    /// <summary>Sets the current culture and UI culture, and puts them back.</summary>
    private sealed class CultureScope : IDisposable
    {
        private readonly CultureInfo _culture = CultureInfo.CurrentCulture;
        private readonly CultureInfo _uiCulture = CultureInfo.CurrentUICulture;

        public CultureScope(string name)
        {
            CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(name);
        }

        public void Dispose()
        {
            CultureInfo.CurrentCulture = _culture;
            CultureInfo.CurrentUICulture = _uiCulture;
        }
    }
}
