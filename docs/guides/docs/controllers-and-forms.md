# Controllers, Navigation and Forms

## Controllers and actions

A controller is a class whose name ends with `Controller` and which inherits from `Controller`. Every public method
that returns `ActionResult` (or `Task<ActionResult>` / `ValueTask<ActionResult>`, see
[Async Actions and Middleware](async-and-middleware.md)) is an action:

```csharp
public class HomeController(IGreetingService greetings) : Controller
{
    public ActionResult Index() => View(new HomeViewModel { Title = greetings.Greet("Ada", null) });

    public ActionResult Settings() => RedirectToAction("Index");   // navigate without rendering

    [NonAction]
    public string Helper() => "not routable";
}
```

- `HomeController` is reached as `Home`; action names are matched case-insensitively and must be unique (no overloads).
- Constructor parameters come from `builder.Services` (see [Dependency Injection](dependency-injection.md)).

| Method | Result |
|---|---|
| `RedirectToAction("Details", new { id })` | Run another action with [route values](#route-values) |
| `View()` / `View(model)` | Render the current action's view (`Views/{Controller}/{Action}View.cvw`) |
| `View("Action")` / `View("Action", model)` | Render another action's view, for example a form again after validation fails |
| `RedirectToAction("Action")` / `RedirectToAction("Action", "Controller")` | Run another action instead of rendering |
| `TryValidateModel(model)` | Validate an object built in code (see [Validation](validation.md)) |

To pass a `string` as the model of the current view, cast it: `View((object)text)`, because `View("text")` names a view.

## Checked when you build

Names written as constants (string literals, `const` fields or `nameof`) are checked against your controllers, actions
and views:

```csharp
return RedirectToAction("Detials");          // warning CVW120: 'Orders' has no action 'Detials'. Actions: Details, Index, …
return View("Sumary", model);                // warning CVW121: there is no view 'Orders/Sumary'
public ActionResult Report() => View();      // warning CVW121 when Views/Orders/ReportView.cvw does not exist
```

The same goes for `NavigationResult.To(...)` and `ToAction(...)` in views, and for `nav` attributes (CVW119). For
CVW121 the IDE offers a fix that creates the view, with `@model` set to the type of the model you pass. Names built at
run time are not checked. To make these warnings errors, add `dotnet_diagnostic.CVW120.severity = error` (and the
same for CVW119 and CVW121) to `.editorconfig`.

## Navigation from views

A view ends by returning a `NavigationResult`, or with a `<menu>`, which returns one for you:

| Method | Meaning |
|---|---|
| `NavigationResult.To("Controller", "Action")` | Go to an action |
| `NavigationResult.To("Controller", "Action", formData)` | Go to an action and post form data |
| `NavigationResult.ToAction("Action")` / `ToAction("Action", formData)` | The same, on the current controller |
| `NavigationResult.To("Orders/Details?id=42")` | Go to an action with [route values](#route-values) |
| `NavigationResult.To("Admin/Users/Index")` | Go to an action in an [area](areas-and-modules.md) |
| `NavigationResult.Back()` | Return to the previous screen (the same as a menu option marked `back`) |
| `NavigationResult.Quit()` | Exit the application |

The framework remembers the screens shown (up to 50). `Back()` and `<option key="b" back>Back</option>` return to the previous one, running its action again without form data; showing the same screen twice in a row (a form after validation fails) counts once. Views read the trail from `NavigationHistory` and show it with `<breadcrumbs />`. See the [design](https://github.com/MateuszPodeszwa/ConsoleMVC/blob/main/docs/design/navigation-history.md).

## Keyboard shortcuts

Map keys that work on every screen in `Program.cs`, after `Build()`:

```csharp
using var app = builder.Build();

app.MapKey(ConsoleKey.F1, "Help/Index", "Help");                                // a route
app.MapKey(new KeyGesture(ConsoleKey.Escape), NavigationResult.Back(), "Back"); // any NavigationResult
app.MapKey(KeyGesture.Parse("Ctrl+X"), NavigationResult.Quit(), "Quit");
app.MapKey(new KeyGesture(ConsoleKey.F5), () => NavigationResult.ToAction("Index")); // decided when pressed
```

Pressing the key while a screen waits for the user (a menu, a text field or an arrow-key field) leaves the screen at
once and follows the navigation. The description is optional; shortcuts with one are listed by `<shortcuts />`.

- Shortcuts can use F1 to F24, Esc, Insert, or any key with Ctrl or Alt. Keys needed for typing are refused, and so are
  Ctrl+C, Ctrl+D, Ctrl+Z and Ctrl+Q and Ctrl+S (terminals keep the last two for flow control).
- With shortcuts mapped, lines are read key by key so shortcuts work in menus and text fields; typing, Backspace,
  Delete, Left, Right, Home and End work as usual.
- With piped input there are no keys to read, so shortcuts are not available.
- A view that catches every exception around a prompt (`catch (Exception)`) would also catch the shortcut; catch
  specific exceptions instead.

In tests, press a shortcut with `new ScriptedInput().Keys(ConsoleKey.F1)` and map it with
`host.ConfigureApplication = app => app.MapKey(...)` (see [Testing](testing.md)).

## Route values

Pass the values an action needs in the navigation itself, like a query string, instead of building a form:

```
<option key="1" nav="Orders/Details?id=@order.Id">@order.Title</option>
```

```csharp
public ActionResult Details(int id) => View(store.Find(id));
public ActionResult Save(OrderForm form) => RedirectToAction("Details", new { id = form.Id });
return NavigationResult.To("Orders", "Details", new { id = 42 });   // from a view's C#
```

- Route values bind to parameters and model properties by name, like form data; when both have a key, the form wins.
- They do not trigger validation (only posted forms are validated), but a value that cannot be converted is still reported in `ModelState`.
- Values from `@expressions` in `nav` are URL-escaped, and values from objects use the invariant culture (`1.5`, `2026-10-27`).
- Back returns to the screen with the same route values.

## Posting forms

`<input name="…">`, `<number>`, `<date>`, `<select>`, `<radio>` and `<checkbox>` store each value in the view's `Form`; post it with a menu option marked `post` or from C#:

```
<input name="Name" label="Name: " required />
<input name="Age" label="Age: " />
<menu>
    <option key="s" nav="Save" post>Save</option>
    <option key="q" quit>Cancel</option>
</menu>
```

Plain C# views can post any `Dictionary<string, string>`: `return NavigationResult.ToAction("Save", formData);`.

## Model binding

The receiving action's parameters are bound from the posted values by name, case-insensitively:

```csharp
public ActionResult Save(SignupModel model) { … }              // properties bound by name
public ActionResult Result(int a, int b, string op) { … }        // simple parameters bound by name
public ActionResult Review(ReviewModel review) { … }             // records: bound through their constructor
```

| Type | Notes |
|---|---|
| `string` | As posted |
| `int`, `long`, `short`, `byte`, `float`, `double`, `decimal`, `Half`, `Int128`, `UInt128` | Invariant culture (`1.5`) |
| `bool` | `true` / `false` |
| `DateTime`, `DateTimeOffset`, `DateOnly`, `TimeOnly`, `TimeSpan` | Invariant culture (`2026-10-27`) |
| `Guid`, `Uri`, `Version` | |
| enums | A name, or a number that is a defined member (or a combination of `[Flags]` members) |
| nullable types (`int?`) | An empty value binds `null` |
| collections: `T[]`, `List<T>`, `IList<T>`, `ICollection<T>`, `IEnumerable<T>`, `IReadOnlyList<T>`, `IReadOnlyCollection<T>` of a type above | The value is split on commas (`<select multiple>` posts `red,blue`); parts are trimmed, empty parts dropped, and an empty value gives an empty collection |
| classes and records | Simple and collection properties by name; records and other classes without a parameterless constructor through their public constructor |
| `CancellationToken` | Never bound from the form: receives the token passed to `RunAsync` |

A missing key leaves the default (or the parameter's default value, `int page = 1`). A value that cannot be converted
keeps the default and is reported in `ModelState`, which is described in [Validation](validation.md).
