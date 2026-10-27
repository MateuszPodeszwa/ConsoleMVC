using System.Reflection;
using ConsoleMVC.Mvc;

namespace ConsoleMVC.Tests;

public class ModelBinderTests
{
    #region Test helpers

    private class SimpleParamsTarget
    {
        public static void NoParams() { }
        public static void StringParam(string name) { }
        public static void IntParam(int age) { }
        public static void BoolParam(bool active) { }
        public static void MultipleParams(string name, int age) { }
        public static void NullableIntParam(int? count) { }
        public static void DefaultValueParam(string name = "default") { }
        public static void DecimalParam(decimal price) { }
        public static void GuidParam(Guid id) { }
    }

    public class PersonModel
    {
        public string Name { get; set; } = "";
        public int Age { get; set; }
        public bool Active { get; set; }
    }

    public class ModelWithNullable
    {
        public int? Score { get; set; }
        public string? Notes { get; set; }
    }

    private class ComplexParamTarget
    {
        public static void WithModel(PersonModel model) { }
        public static void WithNullableModel(ModelWithNullable data) { }
        public static void MixedParams(string title, PersonModel model) { }
    }

    private static MethodInfo GetMethod(Type type, string name) =>
        type.GetMethod(name, BindingFlags.Public | BindingFlags.Static)!;

    #endregion

    // --- No parameters ---

    [Fact]
    public void BindParameters_NoParams_ReturnsNull()
    {
        var method = GetMethod(typeof(SimpleParamsTarget), nameof(SimpleParamsTarget.NoParams));
        var result = ModelBinder.BindParameters(method, new Dictionary<string, string> { ["x"] = "y" });

        Assert.Null(result);
    }

    // --- Simple types ---

    [Fact]
    public void BindParameters_StringParam_BindsByName()
    {
        var method = GetMethod(typeof(SimpleParamsTarget), nameof(SimpleParamsTarget.StringParam));
        var formData = new Dictionary<string, string> { ["name"] = "Alice" };

        var args = ModelBinder.BindParameters(method, formData);

        Assert.NotNull(args);
        Assert.Single(args);
        Assert.Equal("Alice", args[0]);
    }

    [Fact]
    public void BindParameters_IntParam_ConvertsFromString()
    {
        var method = GetMethod(typeof(SimpleParamsTarget), nameof(SimpleParamsTarget.IntParam));
        var formData = new Dictionary<string, string> { ["age"] = "30" };

        var args = ModelBinder.BindParameters(method, formData);

        Assert.NotNull(args);
        Assert.Equal(30, args![0]);
    }

    [Fact]
    public void BindParameters_BoolParam_ConvertsFromString()
    {
        var method = GetMethod(typeof(SimpleParamsTarget), nameof(SimpleParamsTarget.BoolParam));
        var formData = new Dictionary<string, string> { ["active"] = "true" };

        var args = ModelBinder.BindParameters(method, formData);

        Assert.NotNull(args);
        Assert.Equal(true, args![0]);
    }

    [Fact]
    public void BindParameters_MultipleParams_BindsAll()
    {
        var method = GetMethod(typeof(SimpleParamsTarget), nameof(SimpleParamsTarget.MultipleParams));
        var formData = new Dictionary<string, string>
        {
            ["name"] = "Bob",
            ["age"] = "25"
        };

        var args = ModelBinder.BindParameters(method, formData);

        Assert.NotNull(args);
        Assert.Equal(2, args!.Length);
        Assert.Equal("Bob", args[0]);
        Assert.Equal(25, args[1]);
    }

    [Fact]
    public void BindParameters_NullableInt_BindsValue()
    {
        var method = GetMethod(typeof(SimpleParamsTarget), nameof(SimpleParamsTarget.NullableIntParam));
        var formData = new Dictionary<string, string> { ["count"] = "42" };

        var args = ModelBinder.BindParameters(method, formData);

        Assert.Equal(42, args![0]);
    }

    [Fact]
    public void BindParameters_NullableInt_EmptyString_ReturnsNull()
    {
        var method = GetMethod(typeof(SimpleParamsTarget), nameof(SimpleParamsTarget.NullableIntParam));
        var formData = new Dictionary<string, string> { ["count"] = "" };

        var args = ModelBinder.BindParameters(method, formData);

        Assert.Null(args![0]);
    }

    [Fact]
    public void BindParameters_MissingKey_UsesDefault()
    {
        var method = GetMethod(typeof(SimpleParamsTarget), nameof(SimpleParamsTarget.StringParam));
        var formData = new Dictionary<string, string> { ["other"] = "value" };

        var args = ModelBinder.BindParameters(method, formData);

        Assert.Null(args![0]); // string default is null
    }

    [Fact]
    public void BindParameters_MissingKey_IntUsesZero()
    {
        var method = GetMethod(typeof(SimpleParamsTarget), nameof(SimpleParamsTarget.IntParam));
        var formData = new Dictionary<string, string> { ["other"] = "value" };

        var args = ModelBinder.BindParameters(method, formData);

        Assert.Equal(0, args![0]);
    }

    [Fact]
    public void BindParameters_DefaultValueParam_UsesExplicitDefault()
    {
        var method = GetMethod(typeof(SimpleParamsTarget), nameof(SimpleParamsTarget.DefaultValueParam));
        var formData = new Dictionary<string, string>(); // no matching key

        var args = ModelBinder.BindParameters(method, formData);

        Assert.Equal("default", args![0]);
    }

    [Fact]
    public void BindParameters_CaseInsensitiveKeyMatch()
    {
        var method = GetMethod(typeof(SimpleParamsTarget), nameof(SimpleParamsTarget.StringParam));
        var formData = new Dictionary<string, string> { ["NAME"] = "Alice" };

        var args = ModelBinder.BindParameters(method, formData);

        Assert.Equal("Alice", args![0]);
    }

    [Fact]
    public void BindParameters_InvalidIntValue_ReturnsDefault()
    {
        var method = GetMethod(typeof(SimpleParamsTarget), nameof(SimpleParamsTarget.IntParam));
        var formData = new Dictionary<string, string> { ["age"] = "not_a_number" };

        var args = ModelBinder.BindParameters(method, formData);

        Assert.Equal(0, args![0]); // default int
    }

    [Fact]
    public void BindParameters_DecimalParam()
    {
        var method = GetMethod(typeof(SimpleParamsTarget), nameof(SimpleParamsTarget.DecimalParam));
        var formData = new Dictionary<string, string> { ["price"] = "19.99" };

        var args = ModelBinder.BindParameters(method, formData);

        Assert.Equal(19.99m, args![0]);
    }

    [Fact]
    public void BindParameters_GuidParam()
    {
        var guid = Guid.NewGuid();
        var method = GetMethod(typeof(SimpleParamsTarget), nameof(SimpleParamsTarget.GuidParam));
        var formData = new Dictionary<string, string> { ["id"] = guid.ToString() };

        var args = ModelBinder.BindParameters(method, formData);

        Assert.Equal(guid, args![0]);
    }

    [Fact]
    public void BindParameters_NullFormData_ProvideDefaults()
    {
        var method = GetMethod(typeof(SimpleParamsTarget), nameof(SimpleParamsTarget.MultipleParams));

        var args = ModelBinder.BindParameters(method, null);

        Assert.NotNull(args);
        Assert.Null(args![0]); // string
        Assert.Equal(0, args[1]); // int
    }

    // --- Complex types ---

    [Fact]
    public void BindParameters_ComplexType_PopulatesProperties()
    {
        var method = GetMethod(typeof(ComplexParamTarget), nameof(ComplexParamTarget.WithModel));
        var formData = new Dictionary<string, string>
        {
            ["Name"] = "Charlie",
            ["Age"] = "28",
            ["Active"] = "true"
        };

        var args = ModelBinder.BindParameters(method, formData);

        Assert.NotNull(args);
        var model = Assert.IsType<PersonModel>(args![0]);
        Assert.Equal("Charlie", model.Name);
        Assert.Equal(28, model.Age);
        Assert.True(model.Active);
    }

    [Fact]
    public void BindParameters_ComplexType_PartialData()
    {
        var method = GetMethod(typeof(ComplexParamTarget), nameof(ComplexParamTarget.WithModel));
        var formData = new Dictionary<string, string> { ["Name"] = "Diana" };

        var args = ModelBinder.BindParameters(method, formData);

        var model = Assert.IsType<PersonModel>(args![0]);
        Assert.Equal("Diana", model.Name);
        Assert.Equal(0, model.Age); // default
        Assert.False(model.Active); // default
    }

    [Fact]
    public void BindParameters_ComplexType_EmptyFormData()
    {
        var method = GetMethod(typeof(ComplexParamTarget), nameof(ComplexParamTarget.WithModel));
        var formData = new Dictionary<string, string>();

        var args = ModelBinder.BindParameters(method, formData);

        var model = Assert.IsType<PersonModel>(args![0]);
        Assert.Equal("", model.Name); // property default
        Assert.Equal(0, model.Age);
    }

    [Fact]
    public void BindParameters_ComplexType_NullableProperties()
    {
        var method = GetMethod(typeof(ComplexParamTarget), nameof(ComplexParamTarget.WithNullableModel));
        var formData = new Dictionary<string, string>
        {
            ["Score"] = "100",
            ["Notes"] = "Great"
        };

        var args = ModelBinder.BindParameters(method, formData);

        var model = Assert.IsType<ModelWithNullable>(args![0]);
        Assert.Equal(100, model.Score);
        Assert.Equal("Great", model.Notes);
    }

    [Fact]
    public void BindParameters_MixedSimpleAndComplex()
    {
        var method = GetMethod(typeof(ComplexParamTarget), nameof(ComplexParamTarget.MixedParams));
        var formData = new Dictionary<string, string>
        {
            ["title"] = "Welcome",
            ["Name"] = "Eve",
            ["Age"] = "35",
            ["Active"] = "false"
        };

        var args = ModelBinder.BindParameters(method, formData);

        Assert.Equal("Welcome", args![0]);
        var model = Assert.IsType<PersonModel>(args[1]);
        Assert.Equal("Eve", model.Name);
        Assert.Equal(35, model.Age);
        Assert.False(model.Active);
    }

    // --- IsSimpleType ---

    [Theory]
    [InlineData(typeof(string), true)]
    [InlineData(typeof(int), true)]
    [InlineData(typeof(bool), true)]
    [InlineData(typeof(double), true)]
    [InlineData(typeof(decimal), true)]
    [InlineData(typeof(Guid), true)]
    [InlineData(typeof(DateTime), true)]
    [InlineData(typeof(int?), true)]
    [InlineData(typeof(PersonModel), false)]
    [InlineData(typeof(List<string>), false)]
    public void IsSimpleType_ReturnsExpected(Type type, bool expected)
    {
        Assert.Equal(expected, ModelBinder.IsSimpleType(type));
    }

    // --- ConvertValue ---

    [Fact]
    public void ConvertValue_StringToInt()
    {
        Assert.Equal(42, ModelBinder.ConvertValue("42", typeof(int)));
    }

    [Fact]
    public void ConvertValue_InvalidString_ReturnsDefault()
    {
        Assert.Equal(0, ModelBinder.ConvertValue("abc", typeof(int)));
    }

    [Fact]
    public void ConvertValue_EmptyToNullableInt_ReturnsNull()
    {
        Assert.Null(ModelBinder.ConvertValue("", typeof(int?)));
    }

    [Fact]
    public void ConvertValue_StringToString_ReturnsSame()
    {
        Assert.Equal("hello", ModelBinder.ConvertValue("hello", typeof(string)));
    }
}
