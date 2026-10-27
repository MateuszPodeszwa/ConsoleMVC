using System.ComponentModel.DataAnnotations;
using System.Reflection;
using ConsoleMVC.Mvc;
using ConsoleMVC.Tests.Mvc.TestApp;

namespace ConsoleMVC.Tests.Mvc;

/// <summary>
/// Tests for <see cref="ModelStateDictionary"/>, binding errors and DataAnnotations validation.
/// </summary>
public class ValidationTests
{
    public class SignupModel
    {
        [Required, StringLength(5)]
        public string Name { get; set; } = "";

        [Range(13, 120)]
        public int Age { get; set; } = 30;

        [EmailAddress]
        public string? Email { get; set; }
    }

    private static class Targets
    {
        public static void Signup(SignupModel model) { }
        public static void Rate([Range(1, 5)] int stars, [Required] string comment) { }
        public static void Count(int count) { }
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

    // --- ModelStateDictionary ---

    [Fact]
    public void ModelState_StartsValid_AndKeysAreCaseInsensitive()
    {
        var state = new ModelStateDictionary();
        Assert.True(state.IsValid);
        Assert.Empty(state.ErrorsFor("Name"));

        state.AddModelError("Name", "Too short.");
        state.AddModelError("name", "Not allowed.");

        Assert.False(state.IsValid);
        Assert.Equal(2, state.ErrorCount);
        Assert.Equal(["Too short.", "Not allowed."], state.ErrorsFor("NAME"));
        Assert.Single(state);
    }

    [Fact]
    public void ModelState_Clear_MakesItValidAgain()
    {
        var state = new ModelStateDictionary();
        state.AddModelError("", "Something went wrong.");
        state.Clear();

        Assert.True(state.IsValid);
    }

    // --- binding errors ---

    [Fact]
    public void Binding_UnconvertibleSimpleValue_IsReported()
    {
        var (args, state) = BindAndValidate("Count", new() { ["count"] = "abc" });

        Assert.Equal(0, args[0]);
        Assert.Equal(["The value 'abc' is not valid for count."], state.ErrorsFor("count"));
    }

    [Fact]
    public void Binding_UnconvertibleProperty_IsReported_AndNotValidatedAgain()
    {
        var (args, state) = BindAndValidate("Signup", new() { ["Name"] = "Ada", ["Age"] = "old" });

        Assert.Equal(30, Assert.IsType<SignupModel>(args[0]).Age);
        Assert.Equal(["The value 'old' is not valid for Age."], state.ErrorsFor("Age"));
    }

    [Fact]
    public void Binding_EmptyValueForNonNullableNumber_IsNotAConversionError()
    {
        var (_, state) = BindAndValidate("Count", new() { ["count"] = "" });

        Assert.True(state.IsValid);
    }

    // --- DataAnnotations ---

    [Fact]
    public void Model_ValidData_IsValid()
    {
        var (_, state) = BindAndValidate("Signup", new() { ["Name"] = "Ada", ["Age"] = "36", ["Email"] = "ada@example.com" });

        Assert.True(state.IsValid);
    }

    [Fact]
    public void Model_AttributeErrors_AreKeyedByPropertyName()
    {
        var (_, state) = BindAndValidate("Signup", new() { ["Name"] = "", ["Age"] = "7", ["Email"] = "nope" });

        Assert.Equal(["The Name field is required."], state.ErrorsFor("Name"));
        Assert.Single(state.ErrorsFor("Age"));
        Assert.Single(state.ErrorsFor("Email"));
    }

    [Fact]
    public void Model_StringLength_IsChecked()
    {
        var (_, state) = BindAndValidate("Signup", new() { ["Name"] = "Adalbert" });

        Assert.Single(state.ErrorsFor("Name"));
    }

    [Fact]
    public void SimpleParameters_AttributesAreChecked()
    {
        var (_, state) = BindAndValidate("Rate", new() { ["stars"] = "9", ["comment"] = "" });

        Assert.Single(state.ErrorsFor("stars"));
        Assert.Single(state.ErrorsFor("comment"));
    }
}

/// <summary>
/// End-to-end tests: posted forms are validated before the action runs and errors reach the view.
/// </summary>
[Collection(TestAppCollection.Name)]
public class ValidationPipelineTests
{
    private static void Run(string action, Dictionary<string, string>? form)
    {
        Script.Reset();
        var builder = MvcApplication.CreateBuilder([]);
        builder.ApplicationAssembly = typeof(ValidationPipelineTests).Assembly;
        builder.DefaultController = "Shop";
        builder.DefaultAction = "Start";
        builder.Renderer = new ConsoleMVC.Rendering.MarkupRenderer(new ConsoleMVC.Rendering.PlainTextRenderTarget(TextWriter.Null), () => 40);
        Script.Next["Start"] = () => NavigationResult.ToAction(action, form!);
        Script.Next["Basket"] = NavigationResult.Quit;
        builder.Build().Run();
    }

    [Fact]
    public void InvalidPost_ActionSeesErrors_AndViewReceivesThem()
    {
        Run("Checkout", new() { ["Quantity"] = "abc" });

        Assert.Contains("action:Checkout(invalid:2)", Script.Log);
        Assert.Contains("view:Basket errors:2", Script.Log);
    }

    [Fact]
    public void ValidPost_IsValid()
    {
        Run("Checkout", new() { ["Quantity"] = "3", ["Email"] = "ada@example.com" });

        Assert.Contains("action:Checkout(valid)", Script.Log);
    }

    [Fact]
    public void ViewByName_RendersAnotherActionsView()
    {
        Run("Checkout", new() { ["Quantity"] = "0" });

        Assert.Contains("view:Basket errors:2", Script.Log);
    }
}
