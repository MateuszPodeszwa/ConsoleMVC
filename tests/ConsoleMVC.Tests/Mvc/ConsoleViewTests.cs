using ConsoleMVC.Mvc;
using ConsoleMVC.Tests.Mvc.TestApp;
using ConsoleMVC.Tests.Mvc.TestApp.Views.Shop;

namespace ConsoleMVC.Tests.Mvc;

/// <summary>
/// Regression tests for model handling in <see cref="ConsoleView{TModel}"/> (design doc §8, bug 9).
/// </summary>
[Collection(TestAppCollection.Name)]
public class ConsoleViewTests
{
    [Fact]
    public void RenderInternal_NullModel_ForReferenceType_IsAllowed()
    {
        Script.Reset();

        var result = new IndexView().RenderInternal(null);

        Assert.True(result.Exit);
    }

    [Fact]
    public void RenderInternal_NullModel_ForValueType_ThrowsDescriptiveError()
    {
        var error = Assert.Throws<InvalidOperationException>(() => new CountView().RenderInternal(null));

        Assert.Contains("CountView", error.Message);
        Assert.Contains("Int32", error.Message);
    }

    [Fact]
    public void RenderInternal_WrongModelType_ThrowsDescriptiveError()
    {
        var error = Assert.Throws<InvalidOperationException>(() => new BasketView().RenderInternal(42));

        Assert.Contains("BasketView", error.Message);
        Assert.Contains(nameof(OrderModel), error.Message);
        Assert.Contains("Int32", error.Message);
    }
}
