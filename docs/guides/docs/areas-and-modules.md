# Areas and Modules

## Areas

An area groups related screens: their controllers, their views and their routes. Put the files under
`Areas/{Area}`:

```
MyApp/
├── Controllers/HomeController.cs
├── Views/
│   ├── Home/IndexView.cvw
│   └── Shared/_Layout.cvw                   (used by area views too)
└── Areas/
    └── Admin/
        ├── Controllers/UsersController.cs   (namespace MyApp.Areas.Admin.Controllers)
        └── Views/Users/IndexView.cvw
```

A controller whose namespace contains `.Areas.{Area}.` is in that area. Anywhere else, put it in one with an
attribute:

```csharp
[Area("Admin")]
public class UsersController : Controller
{
    public ActionResult Index() => View();       // Areas/Admin/Views/Users/IndexView.cvw
}
```

### Routes

An area's routes start with the area:

```
<option key="a" nav="Admin/Users/Index">Users</option>
```

```csharp
return RedirectToAction("Index", "Admin/Users");
return NavigationResult.To("Admin/Users/Details?id=4");
```

Inside an area, a controller named without an area means the area's own controller when it has one, and the
application's otherwise. So from a screen in the Admin area, `nav="Home/Index"` goes to `Admin/Home` if the area has a
Home controller, and to the application's `Home` if not. Two areas can each have a `HomeController`.

`RouteContext.Area` is the area of a route (`null` outside one), and `RouteContext.ControllerName` is the controller
without it. Breadcrumbs show the controller's name without the area.

### Views and partials

- Views go in `Areas/{Area}/Views/{Controller}`; the build checks area routes in `nav`, `RedirectToAction` and
  `NavigationResult` like any other (CVW119 to CVW121), and the Create view fix creates area views in the right folder.
- Partials and layouts live in the project's `Views/Shared` and are shared by every area.

## Modules

A module is a class library with its own controllers, views and services, added to an application:

```csharp
// In the library (it references ConsoleMVC.Framework, so its .cvw views are compiled there)
public sealed class BillingModule : IMvcModule
{
    public void Configure(MvcApplicationBuilder builder)
    {
        builder.Services.AddSingleton<IInvoiceStore, InvoiceStore>();
    }
}

// In the application's Program.cs
builder.AddModule<BillingModule>();
```

- The module's controllers and views are discovered with the application's (and again after a hot reload).
- `Configure` runs once, when the module is added; adding a module twice does nothing.
- `builder.AddModule(assembly)` adds a library that has no module class.
- Put a module's controllers in an area named after it (`[Area("Billing")]`), so its routes cannot collide with the
  application's. Their views then live where an area's views do, in the library's `Areas/Billing/Views/{Controller}`
  (`Areas/Billing/Views/Invoices/IndexView.cvw`); views in `Views/Invoices` are not found, and the library's build
  warns about them with CVW121. A module can have its own `Views/Shared` partials for its views.
- The build checks know both sides: the application's `nav`, `RedirectToAction` and `MapKey` routes may name the
  modules' controllers (`nav="Billing/Invoices/Index"`), and a module may name the application's (`nav="Home/Index"`),
  which it cannot see, so those are not reported as unknown; its own controllers' actions are still checked. A library
  with an `IMvcModule` class is a module for these checks; one added with `AddModule(assembly)` says so in its project
  file:

  ```xml
  <PropertyGroup>
      <ConsoleMVCModule>true</ConsoleMVCModule>
  </PropertyGroup>
  ```
