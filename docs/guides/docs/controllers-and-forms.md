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
| `View()` / `View(model)` | Render the current action's view (`Views/{Controller}/{Action}View.cvw`) |
| `View("Action")` / `View("Action", model)` | Render another action's view, for example a form again after validation fails |
| `RedirectToAction("Action")` / `RedirectToAction("Action", "Controller")` | Run another action instead of rendering |
| `TryValidateModel(model)` | Validate an object built in code (see [Validation](validation.md)) |

To pass a `string` as the model of the current view, cast it: `View((object)text)`, because `View("text")` names a view.

## Navigation from views

A view ends by returning a `NavigationResult`, or with a `<menu>`, which returns one for you:

| Method | Meaning |
|---|---|
| `NavigationResult.To("Controller", "Action")` | Go to an action |
| `NavigationResult.To("Controller", "Action", formData)` | Go to an action and post form data |
| `NavigationResult.ToAction("Action")` / `ToAction("Action", formData)` | The same, on the current controller |
| `NavigationResult.Back()` | Return to the previous screen (the same as a menu option marked `back`) |
| `NavigationResult.Quit()` | Exit the application |

The framework remembers the screens shown (up to 50). `Back()` and `<option key="b" back>Back</option>` return to the previous one, running its action again without form data; showing the same screen twice in a row (a form after validation fails) counts once. Views read the trail from `NavigationHistory` and show it with `<breadcrumbs />`. See the [design](https://github.com/MateuszPodeszwa/ConsoleMVC/blob/main/docs/design/navigation-history.md).

## Posting forms

`<input name="…">` stores each value in the view's `Form`; post it with a menu option marked `post` or from C#:

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
| classes and records | Simple properties by name; records and other classes without a parameterless constructor through their public constructor |
| `CancellationToken` | Never bound from the form: receives the token passed to `RunAsync` |

A missing key leaves the default (or the parameter's default value, `int page = 1`). A value that cannot be converted
keeps the default and is reported in `ModelState`, which is described in [Validation](validation.md).
