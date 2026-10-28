using System.Reflection;
using ConsoleMVC.Generators.Tests.Infrastructure;
using ConsoleMVC.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace ConsoleMVC.Generators.Tests;

/// <summary>Tests for the generated route table used for trimming and Native AOT (docs/design/native-aot.md).</summary>
public class RouteTableTests
{
    private const string Source = """
        using System.Collections.Generic;
        using System.Threading.Tasks;
        using ConsoleMVC.Mvc;

        namespace TestApp
        {
            public interface IClock { string Now { get; } }
            public sealed class FixedClock : IClock { public string Now => "noon"; }

            public sealed class Address { public string Street { get; set; } = ""; }
            public sealed class Order
            {
                public string Name { get; set; } = "";
                public List<Line> Lines { get; set; } = new();
                public Address Delivery { get; set; } = new();
            }
            public sealed class Line { public int Quantity { get; set; } }
        }

        namespace TestApp.Controllers
        {
            public class ShopController : Controller
            {
                private readonly TestApp.IClock _clock;
                public ShopController(TestApp.IClock clock) => _clock = clock;

                public ActionResult Index() => View((object)_clock.Now);
                public async Task<ActionResult> Later(int minutes) { await Task.Yield(); return RedirectToAction("Index"); }
                public ValueTask<ViewResult> Soon() => new(View("Index", (object)"soon"));
                public ActionResult Save(TestApp.Order order, string note) => View("Index", (object)(order.Name + note));
            }

            public class FlexibleController : Controller
            {
                public FlexibleController() { }
                public FlexibleController(TestApp.IClock clock) { }
                public ActionResult Index() => View();
            }
        }

        namespace TestApp.Views.Flexible
        {
            public sealed class IndexView : ConsoleView<object>
            {
                public override NavigationResult Render(object Model) => NavigationResult.Quit();
            }
        }
        """;

    private static readonly (string, string)[] Views =
    [
        ("/app/Views/Shop/IndexView.cvw", "@model string\n@inject TestApp.IClock Clock\n<text>@Model at @Clock.Now</text>\nreturn NavigationResult.Quit();")
    ];

    private static (Assembly Assembly, string Table) Build()
    {
        var result = ViewHarness.GenerateFiles(Views, Source);
        Assert.True(result.Assembly is not null && !result.Errors.Any(), result.Describe());
        return (result.Assembly!, ViewHarness.LastRouteTable!);
    }

    [Fact]
    public void TheAssembly_NamesItsRouteTable_AndEveryCompiledView()
    {
        var (assembly, _) = Build();

        var table = (IRouteTable)Activator.CreateInstance(assembly.GetCustomAttribute<RouteTableAttribute>()!.TableType)!;
        Assert.Equal(["Flexible", "Shop"], table.Controllers.Select(c => c.Name).Order());
        Assert.Equal(["Flexible/Index"], table.Views.Select(v => v.Key)); // the hand-written view
        Assert.Equal(["Shop/Index"], assembly.GetCustomAttributes<GeneratedViewAttribute>().Select(v => v.Key)); // the .cvw view
    }

    [Fact]
    public async Task Controllers_AreCreated_AndActionsCalled_WithoutReflection()
    {
        var (assembly, _) = Build();
        var clockType = assembly.GetType("TestApp.FixedClock")!;
        var clockInterface = assembly.GetType("TestApp.IClock")!;
        var services = new ServiceCollection().AddSingleton(clockInterface, clockType).BuildServiceProvider();

        var router = new Router();
        router.DiscoverAll(assembly);
        var shop = assembly.GetCustomAttribute<RouteTableAttribute>()!.TableType;
        var route = ((IRouteTable)Activator.CreateInstance(shop)!).Controllers.Single(c => c.Name == "Shop");
        Assert.NotNull(route.Create);                                       // one public constructor: a typed factory

        var controller = router.ResolveController("Shop", services);
        foreach (var (action, args) in new (string, object?[])[] { ("Index", []), ("Later", [5]), ("Soon", []) })
        {
            var method = router.ResolveAction(controller.GetType(), action);
            var invoker = router.InvokerFor(method);
            Assert.NotNull(invoker);
            Assert.NotNull(await invoker!(controller, args));
        }
    }

    [Fact]
    public void ControllersWithSeveralConstructors_AreLeftToActivatorUtilities()
    {
        var (assembly, _) = Build();
        var table = (IRouteTable)Activator.CreateInstance(assembly.GetCustomAttribute<RouteTableAttribute>()!.TableType)!;

        Assert.Null(table.Controllers.Single(c => c.Name == "Flexible").Create);

        var router = new Router();
        router.DiscoverAll(assembly);
        Assert.NotNull(router.ResolveController("Flexible", new ServiceCollection().BuildServiceProvider()));
    }

    [Fact]
    public void GeneratedViews_FillTheirInjectedServices_Themselves()
    {
        var (assembly, _) = Build();
        var services = new ServiceCollection()
            .AddSingleton(assembly.GetType("TestApp.IClock")!, assembly.GetType("TestApp.FixedClock")!).BuildServiceProvider();

        var router = new Router();
        router.DiscoverAll(assembly);
        var view = router.ResolveView("Shop", "Index", services);

        Assert.True(view.TryInjectServices(services));
        Assert.NotNull(view.GetType().GetProperty("Clock")!.GetValue(view));
    }

    [Fact]
    public void ModelTypes_AreKeptForTrimming_WithTheirPropertiesAndCollections()
    {
        var (_, table) = Build();

        foreach (var type in new[] { "global::TestApp.Order", "global::TestApp.Line", "global::TestApp.Address" })
            Assert.Contains($"PublicConstructors, typeof({type}))]", table);
        Assert.DoesNotContain("typeof(string))]", table);                    // simple types need nothing
        Assert.DoesNotContain("typeof(int))]", table);
    }

    [Fact]
    public void AProjectWithOnlyViews_StillGetsATable()
    {
        var result = ViewHarness.Generate("<text>x</text>\nreturn NavigationResult.Quit();");

        Assert.True(result.Assembly is not null && !result.Errors.Any(), result.Describe());
        Assert.NotNull(result.Assembly!.GetCustomAttribute<RouteTableAttribute>());
        Assert.Single(result.Assembly.GetCustomAttributes<GeneratedViewAttribute>());
    }

    [Fact]
    public void AViewWithErrors_IsNotRegistered()
    {
        var result = ViewHarness.Generate("<box>");

        Assert.Empty(result.Assembly?.GetCustomAttributes<GeneratedViewAttribute>() ?? []);
        Assert.DoesNotContain("GeneratedView", result.GeneratedSource ?? "");
    }
}
