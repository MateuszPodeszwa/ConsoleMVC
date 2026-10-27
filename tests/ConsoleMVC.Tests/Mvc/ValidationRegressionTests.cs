using System.ComponentModel.DataAnnotations;
using System.Reflection;
using ConsoleMVC.Mvc;
using ConsoleMVC.Rendering;

namespace ConsoleMVC.Tests.Mvc;

/// <summary>
/// Regression tests for form validation bugs found in review.
/// </summary>
public class ValidationRegressionTests
{
    public class Booking : IValidatableObject
    {
        public DateOnly Start { get; set; } = new(2026, 1, 1);

        public DateOnly End { get; set; } = new(2026, 1, 1);

        [Range(1, 9)]
        public int Guests { get; set; } = 1;

        public IEnumerable<ValidationResult> Validate(ValidationContext context)
        {
            if (End <= Start)
                yield return new ValidationResult("End must be after Start.", [nameof(Start)]);
        }
    }

    public class Survey
    {
        [Display(Name = "Your age"), Range(1, 9)]
        public int Age { get; set; } = 50;
    }

    public record Review([property: Range(1, 5)] int Stars, string Comment);

    public record Unbindable(Review Inner);

    private static class Targets
    {
        public static void Book(Booking booking) { }
        public static void Answer(Survey survey) { }
        public static void Rate([Display(Name = "Stars count")] int stars) { }
        public static void Post(Review review) { }
        public static void Broken(Unbindable value) { }
    }

    public sealed class ProbeController : Controller
    {
        public ProbeController() => RouteContext = new RouteContext { Controller = "Probe", Action = "Save" };

        public ViewResult Named() => View("Index");

        public ViewResult WithModel() => View((object)"model");

        public bool Validate(object model) => TryValidateModel(model);
    }

    private static MethodInfo Method(string name) =>
        typeof(Targets).GetMethod(name, BindingFlags.Public | BindingFlags.Static)!;

    private static (object?[] Args, ModelStateDictionary State) BindAndValidate(string method, Dictionary<string, string> form)
    {
        var state = new ModelStateDictionary();
        var args = ModelBinder.BindParameters(Method(method), form, state)!;
        ModelValidator.Validate(Method(method), args, state);
        return (args, state);
    }

    [Fact]
    public void ConversionError_SkipsObjectLevelValidation_ButKeepsOtherPropertyChecks()
    {
        var (_, state) = BindAndValidate("Book",
            new() { ["Start"] = "2026-03-01", ["End"] = "2026-04-31", ["Guests"] = "20" });

        Assert.Equal(["End", "Guests"], state.Keys.Order());
        Assert.Single(state.ErrorsFor("End"));
        Assert.Empty(state.ErrorsFor("Start")); // the cross-field check would compare a fallback value
    }

    [Fact]
    public void ObjectLevelValidation_StillRunsWithoutConversionErrors()
    {
        var (_, state) = BindAndValidate("Book", new() { ["Start"] = "2026-03-01", ["End"] = "2026-02-01" });

        Assert.Equal(["End must be after Start."], state.ErrorsFor("Start"));
    }

    [Fact]
    public void TryValidateModel_DoesNotDuplicateErrors_AndReportsTheModelsValidity()
    {
        var controller = new ProbeController();
        var survey = new Survey();

        Assert.False(controller.Validate(survey));
        Assert.False(controller.Validate(survey));

        Assert.Single(controller.ModelState.ErrorsFor("Age"));
        Assert.True(controller.Validate(new Booking { End = new DateOnly(2027, 1, 1) }));
    }

    [Fact]
    public void ViewWithOneString_RendersTheNamedView()
    {
        var result = new ProbeController().Named();

        Assert.Equal("Index", result.ViewName);
        Assert.Null(result.Model);
    }

    [Fact]
    public void ViewWithAStringModel_NeedsACastToObject()
    {
        var result = new ProbeController().WithModel();

        Assert.Equal("Save", result.ViewName);
        Assert.Equal("model", result.Model);
    }

    [Fact]
    public void PositionalRecords_AreBoundThroughTheirConstructor_AndValidated()
    {
        var (args, state) = BindAndValidate("Post", new() { ["stars"] = "9", ["Comment"] = "Great" });

        var review = Assert.IsType<Review>(args[0]);
        Assert.Equal(new Review(9, "Great"), review);
        Assert.Contains("Stars", state.Keys);
    }

    [Fact]
    public void PositionalRecords_ReportConversionErrors()
    {
        var (args, state) = BindAndValidate("Post", new() { ["Stars"] = "many" });

        Assert.Equal(new Review(0, null!), args[0]);
        Assert.Single(state.ErrorsFor("Stars"));
    }

    [Fact]
    public void ModelsThatCannotBeCreated_Throw()
    {
        var error = Assert.Throws<InvalidOperationException>(() => BindAndValidate("Broken", new()));

        Assert.Contains(nameof(Unbindable), error.Message);
    }

    [Fact]
    public void ConversionErrors_UseTheDisplayName()
    {
        Assert.Equal(["The value 'x' is not valid for Your age."],
            BindAndValidate("Answer", new() { ["Age"] = "x" }).State.ErrorsFor("Age"));

        Assert.Equal(["The value 'x' is not valid for Stars count."],
            BindAndValidate("Rate", new() { ["stars"] = "x" }).State.ErrorsFor("stars"));
    }

    [Fact]
    public void ErrorsElement_MeasuresLikeText()
    {
        var errors = new ModelStateDictionary();
        errors.AddModelError("A", "ab\ncd");
        errors.AddModelError("B", "ef    ");
        var element = new ValidationErrorsElement { Source = errors };

        Assert.Equal(ValidationErrorsElement.Bullet.Length + 2, element.MeasureWidth(80));
    }
}
