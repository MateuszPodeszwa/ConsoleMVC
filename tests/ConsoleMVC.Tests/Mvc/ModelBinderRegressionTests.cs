using System.Reflection;
using ConsoleMVC.Mvc;

namespace ConsoleMVC.Tests.Mvc;

/// <summary>
/// Regression tests for <see cref="ModelBinder"/> (design doc §8, bugs 7 and 8).
/// </summary>
public class ModelBinderRegressionTests
{
    public class SignupModel
    {
        public string Name { get; set; } = "anonymous";
        public int Age { get; set; } = 18;
        public bool Newsletter { get; set; } = true;
        public int? Referrals { get; set; } = 5;
    }

    private static class Targets
    {
        public static void Signup(SignupModel model) { }
        public static void Paged(int page = 1) { }
    }

    private static MethodInfo Method(string name) =>
        typeof(Targets).GetMethod(name, BindingFlags.Public | BindingFlags.Static)!;

    [Fact]
    public void ComplexType_MissingKeys_KeepPropertyInitialisers()
    {
        var args = ModelBinder.BindParameters(Method("Signup"), new Dictionary<string, string> { ["Name"] = "Ada" })!;
        var model = Assert.IsType<SignupModel>(args[0]);

        Assert.Equal("Ada", model.Name);
        Assert.Equal(18, model.Age);
        Assert.True(model.Newsletter);
        Assert.Equal(5, model.Referrals);
    }

    [Fact]
    public void ComplexType_InvalidValue_KeepsPropertyInitialiser()
    {
        var args = ModelBinder.BindParameters(Method("Signup"), new Dictionary<string, string> { ["Age"] = "eighteen" })!;

        Assert.Equal(18, Assert.IsType<SignupModel>(args[0]).Age);
    }

    [Fact]
    public void ComplexType_EmptyValue_ForNullable_SetsNull()
    {
        var args = ModelBinder.BindParameters(Method("Signup"), new Dictionary<string, string> { ["Referrals"] = "" })!;

        Assert.Null(Assert.IsType<SignupModel>(args[0]).Referrals);
    }

    [Fact]
    public void SimpleParameter_InvalidValue_UsesDeclaredDefault()
    {
        var args = ModelBinder.BindParameters(Method("Paged"), new Dictionary<string, string> { ["page"] = "abc" })!;

        Assert.Equal(1, args[0]);
    }

    [Fact]
    public void Keys_DifferingOnlyByCase_DoNotThrow()
    {
        var formData = new Dictionary<string, string> { ["page"] = "2", ["PAGE"] = "3" };

        var args = ModelBinder.BindParameters(Method("Paged"), formData)!;

        Assert.Equal(2, args[0]); // first matching key wins
    }
}
