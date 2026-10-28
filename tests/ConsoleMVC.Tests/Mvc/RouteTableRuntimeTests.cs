using ConsoleMVC.Mvc;
using ConsoleMVC.Tests.Mvc.KioskApp;
using ConsoleMVC.Tests.Mvc.KioskApp.Controllers;
using Microsoft.Extensions.DependencyInjection;

namespace ConsoleMVC.Tests.Mvc;

/// <summary>Tests for the router's use of an <see cref="IRouteTable"/> (docs/design/native-aot.md).</summary>
public class RouteTableRuntimeTests
{
    /// <summary>A hand-written table for the kiosk's controller, counting what it creates and calls.</summary>
    private sealed class CountingTable : IRouteTable
    {
        public int Created;
        public int Called;

        public CountingTable()
        {
            Controllers =
            [
                new ControllerRoute("Kiosk", typeof(KioskController), services =>
                {
                    Created++;
                    return new KioskController(services.GetRequiredService<IKioskGreeter>());
                },
                [
                    new ActionRoute("Index", typeof(KioskController).GetMethod(nameof(KioskController.Index))!, (c, _) =>
                    {
                        Called++;
                        return ActionRoute.FromResult(((KioskController)c).Index());
                    })
                ])
            ];
        }

        public IReadOnlyList<ControllerRoute> Controllers { get; }

        public IReadOnlyList<ViewRoute> Views { get; } =
            [new ViewRoute("Kiosk/Index", typeof(KioskApp.Views.Kiosk.IndexView), () => new KioskApp.Views.Kiosk.IndexView())];
    }

    [Fact]
    public async Task Use_CreatesAndCallsThroughTheTable()
    {
        var table = new CountingTable();
        var router = new Router();
        router.Use(table);
        var services = new ServiceCollection().AddSingleton<IKioskGreeter, KioskGreeter>().BuildServiceProvider();

        var controller = router.ResolveController("kiosk", services);
        var method = router.ResolveAction(controller.GetType(), "index");
        var result = await router.InvokerFor(method)!(controller, null);

        Assert.Equal(1, table.Created);
        Assert.Equal(1, table.Called);
        Assert.IsType<ViewResult>(result);
        Assert.IsType<KioskApp.Views.Kiosk.IndexView>(router.ResolveView("Kiosk", "Index", services));
    }

    [Fact]
    public void ActionsNotInTheTable_AreFoundByReflection()
    {
        var router = new Router();
        router.Use(new CountingTable());

        var method = router.ResolveAction(typeof(KioskController), "Form");

        Assert.Equal("Form", method.Name);
        Assert.Null(router.InvokerFor(method)); // called by reflection
    }

    [Fact]
    public void AssembliesWithoutATable_AreScanned_AndScanningCanBeChosen()
    {
        var router = new Router();
        router.DiscoverAll(typeof(KioskController).Assembly, useRouteTable: false); // as after a hot reload

        Assert.NotNull(router.ResolveController("Kiosk", new ServiceCollection().AddSingleton<IKioskGreeter, KioskGreeter>().BuildServiceProvider()));
    }
}
