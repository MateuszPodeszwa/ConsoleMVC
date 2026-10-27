using System.Reflection;
using ConsoleMVC.Mvc;

namespace ConsoleMVC.Tests;

/// <summary>
/// Tests for binding comma-separated form values, such as the answer of <c>&lt;select multiple&gt;</c>, to collections.
/// </summary>
public class CollectionBindingTests
{
    public enum Topping
    {
        Cheese,
        Ham,
        Olives
    }

    public class OrderForm
    {
        public string[] Tags { get; set; } = [];
        public List<int> Ids { get; set; } = [];
        public IReadOnlyList<Topping> Toppings { get; set; } = [];
        public IEnumerable<string>? Notes { get; set; }
        public bool Newsletter { get; set; }
    }

    private static class Actions
    {
        public static void Order(OrderForm form) { }
        public static void Ids(int[] ids) { }
        public static void Names(List<string> names) { }
    }

    private static object?[] Bind(string method, Dictionary<string, string> form, ModelStateDictionary? state = null) =>
        ModelBinder.BindParameters(typeof(Actions).GetMethod(method, BindingFlags.Public | BindingFlags.Static)!, form, state)!;

    [Fact]
    public void ModelProperties_SplitTheValueOnCommas()
    {
        var form = (OrderForm)Bind("Order", new()
        {
            ["Tags"] = "a, b ,c",
            ["Ids"] = "3,1",
            ["Toppings"] = "Ham,olives",
            ["Notes"] = "x",
            ["Newsletter"] = "true"
        })[0]!;

        Assert.Equal(["a", "b", "c"], form.Tags);
        Assert.Equal([3, 1], form.Ids);
        Assert.Equal([Topping.Ham, Topping.Olives], form.Toppings);
        Assert.Equal(["x"], form.Notes!);
        Assert.True(form.Newsletter);
    }

    [Fact]
    public void EmptyValues_GiveEmptyCollections_AndMissingOnesKeepTheInitialValue()
    {
        var form = (OrderForm)Bind("Order", new() { ["Tags"] = "", ["Ids"] = " , " })[0]!;

        Assert.Empty(form.Tags);
        Assert.Empty(form.Ids);
        Assert.Null(form.Notes);
    }

    [Fact]
    public void Parameters_BindCollectionsToo()
    {
        Assert.Equal([1, 2], (int[])Bind("Ids", new() { ["ids"] = "1,2" })[0]!);
        Assert.Equal(["Ada", "Grace"], (List<string>)Bind("Names", new() { ["names"] = "Ada,Grace" })[0]!);
    }

    [Fact]
    public void ABadPart_IsAConversionError()
    {
        var state = new ModelStateDictionary();

        var form = (OrderForm)Bind("Order", new() { ["Ids"] = "1,two" }, state)[0]!;

        Assert.False(state.IsValid);
        Assert.NotEmpty(state.ErrorsFor("Ids"));
        Assert.Empty(form.Ids);
    }
}
