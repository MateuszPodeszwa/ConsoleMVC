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

/// <summary>
/// Regression tests for types and values the binder previously ignored or accepted wrongly.
/// </summary>
public class ModelBinderTypeTests
{
    public enum Size
    {
        Small,
        Large
    }

    [Flags]
    public enum Access
    {
        None = 0,
        Read = 1,
        Write = 2
    }

    public class Booking
    {
        public DateOnly Date { get; set; }
        public TimeOnly Time { get; set; }
        public Size Size { get; set; } = Size.Large;
    }

    private static class Targets
    {
        public static void Simple(DateOnly date, TimeOnly time, Uri? site, Int128 big, Half ratio) { }
        public static void Model(Booking booking) { }
        public static void Sized(Size size = Size.Large) { }
        public static void Permissions(Access access) { }
    }

    private static object?[] Bind(string method, Dictionary<string, string> form) =>
        ModelBinder.BindParameters(typeof(Targets).GetMethod(method)!, form)!;

    [Fact]
    public void ModernValueTypes_AndUri_AreBound()
    {
        var args = Bind("Simple", new()
        {
            ["date"] = "2026-10-27", ["time"] = "13:45", ["site"] = "https://example.com", ["big"] = "170141183460469231731687303715884105727", ["ratio"] = "0.5"
        });

        Assert.Equal(new DateOnly(2026, 10, 27), args[0]);
        Assert.Equal(new TimeOnly(13, 45), args[1]);
        Assert.Equal(new Uri("https://example.com"), args[2]);
        Assert.Equal(Int128.MaxValue, args[3]);
        Assert.Equal((Half)0.5, args[4]);
    }

    [Fact]
    public void ModernValueTypes_AreBound_OnModels()
    {
        var booking = Assert.IsType<Booking>(Bind("Model", new() { ["Date"] = "2026-10-27", ["Time"] = "09:30" })[0]);

        Assert.Equal(new DateOnly(2026, 10, 27), booking.Date);
        Assert.Equal(new TimeOnly(9, 30), booking.Time);
    }

    [Theory]
    [InlineData("99")]
    [InlineData("-1")]
    public void UndefinedEnumNumbers_AreRejected(string value)
    {
        Assert.Equal(Size.Large, Bind("Sized", new() { ["size"] = value })[0]);
        Assert.Equal(Size.Large, Assert.IsType<Booking>(Bind("Model", new() { ["Size"] = value })[0]).Size);
    }

    [Fact]
    public void DefinedEnumNumbers_AndFlagCombinations_AreAccepted()
    {
        Assert.Equal(Size.Small, Bind("Sized", new() { ["size"] = "0" })[0]);
        Assert.Equal(Access.Read | Access.Write, Bind("Permissions", new() { ["access"] = "3" })[0]);
    }
}
