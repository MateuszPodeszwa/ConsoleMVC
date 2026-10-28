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

The same goes for `NavigationResult.To(...)` and `ToAction(...)` in views, for the routes of `app.MapKey(key, "Help/Index")`, and for `nav` attributes (CVW119). The message suggests the closest name (`Did you mean 'Details'?`) and lists the others, closest first. For
CVW121 the IDE offers a fix that creates the view, with `@model` set to the type of the model you pass. Names built at
run time are not checked. To make these warnings errors, add
`<WarningsAsErrors>$(WarningsAsErrors);CVW119;CVW120;CVW121</WarningsAsErrors>` to the project file. CVW120 and
CVW121 also follow `dotnet_diagnostic.CVW120.severity = error` in `.editorconfig`; CVW119 is reported against the
`.cvw` view, which `.editorconfig` sections do not cover, so set it in the project file or in a `.globalconfig` file
(`is_global = true`).

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

The framework remembers the screens shown (up to 50). `Back()` and `<option key="b" back>Back</option>` return to the previous one, running its action again without form data; a screen is its controller, action and route values, so `Orders/Details?id=2` and `Orders/Details?id=42` are two screens. Showing the same screen twice in a row (a form after validation fails, a refresh) counts once, and going to a screen that is already in the history (a menu's Home option) returns the history to it: the breadcrumbs are always the path to the current screen. Views read the trail from `NavigationHistory` and show it with `<breadcrumbs />`. See the [design](https://github.com/MateuszPodeszwa/ConsoleMVC/blob/main/docs/design/navigation-history.md).

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

- A field keeps a key it has a use for at that moment: Esc restores a changed `<number>` and clears a `<select filter>`'s
  filter, and is the shortcut otherwise.
- Shortcuts can use F1 to F24, Esc, Insert, or any key with Ctrl or Alt. Keys needed for typing are refused, and so are
  Ctrl+C (it interrupts), Ctrl+D and Ctrl+Z (they end the input), Ctrl+H, Ctrl+I, Ctrl+J and Ctrl+M (terminals send them
  as Backspace, Tab and Enter, so they would never reach the shortcut), and Ctrl+Q and Ctrl+S (flow control), with or
  without Shift.
- With shortcuts mapped, lines are read key by key so shortcuts work in menus and text fields; typing, Backspace,
  Delete, Left, Right, Home and End work as usual.
- With piped input there are no keys to read, so shortcuts are not available.
- A view that catches every exception around a prompt (`catch (Exception)`) would also catch the shortcut; catch
  specific exceptions instead.

In tests, press a shortcut with `new ScriptedInput().Keys(ConsoleKey.F1)` and map it with
`host.ConfigureApplication = app => app.MapKey(...)` (see [Testing](testing.md)).

## Mouse

```csharp
builder.Mouse = true;          // or let users choose: --ConsoleMVC:Mouse=true
```

In terminals that report the mouse (most do; the legacy Windows console does not):

| Click | Does |
|---|---|
| a `<menu>` option | chooses it (whatever was typed is replaced) |
| a `<select>` or vertical `<radio>` choice | chooses it (ticks it in a `<select multiple>`) |
| a `<checkbox>` | ticks or clears it, as Space does |
| the wheel | moves through lists and `<scroll>` like the arrow keys |

Typing still works as before, and keyboard shortcuts work alongside the mouse. In tests, `ScriptedInput.Click(column,
row)` and `Wheel(down)` send what a terminal would; see [Testing](testing.md). See the
[design](https://github.com/MateuszPodeszwa/ConsoleMVC/blob/main/docs/design/mouse.md).

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
| `string` | As posted; an empty value is not bound, so the field keeps its default (`null`, or its initialiser) |
| `int`, `long`, `short`, `byte`, `float`, `double`, `decimal`, `Half`, `Int128`, `UInt128` | Invariant culture (`1.5`, `-2`, `1.5e3`), decimal digits only: hexadecimal (`0x10`), thousands separators (`1,000`) and values that are not finite (`1e400`, `NaN`) are not valid |
| `bool` | `true` / `false` |
| `DateTime`, `DateTimeOffset`, `DateOnly`, `TimeOnly`, `TimeSpan` | Invariant culture (`2026-10-27`) |
| `Guid`, `Uri`, `Version` | |
| enums | A name, or a number that is a defined member (or a combination of `[Flags]` members) |
| nullable types (`int?`) | An empty value binds `null` |
| collections: `T[]`, `List<T>`, `IList<T>`, `ICollection<T>`, `IEnumerable<T>`, `IReadOnlyList<T>`, `IReadOnlyCollection<T>` of a type above | The value is split on commas (`<select multiple>` posts `red,blue`); parts are trimmed, empty parts dropped, and an empty value gives an empty collection |
| classes and records | Simple and collection properties by name; records and other classes without a parameterless constructor through their public constructor |
| `CancellationToken` | Never bound from the form: cancelled when the token passed to `RunAsync` is, or by Ctrl+C |

A missing key or an empty value leaves the default (or the parameter's default value, `int page = 1`), as in ASP.NET
Core, where an empty value binds `null`: an optional `[EmailAddress] string? Email` left blank is valid. A value that cannot be converted
keeps the default and is reported in `ModelState`, which is described in [Validation](validation.md).
