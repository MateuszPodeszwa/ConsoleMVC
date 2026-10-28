# Validation

To show validation messages in the user's language, see [Localisation](localisation.md#validation-messages).

Posted form data is validated **before the action runs** with the standard `System.ComponentModel.DataAnnotations`
attributes, and the results are in `ModelState`, as in ASP.NET Core MVC.

```csharp
public class SignupModel
{
    [Required, StringLength(30)]
    public string Name { get; set; } = "";

    [Display(Name = "Your age"), Range(13, 120)]
    public int Age { get; set; } = 18;

    [EmailAddress]
    public string? Email { get; set; }
}

public class AccountController : Controller
{
    public ActionResult Signup() => View();

    public ActionResult Save(SignupModel model)
    {
        if (!ModelState.IsValid)
            return View("Signup", model);             // the form again, with its errors

        if (users.Exists(model.Name))
        {
            ModelState.AddModelError("Name", "That name is taken.");
            return View("Signup", model);
        }

        return RedirectToAction("Welcome");
    }

    public ActionResult Rate([Range(1, 5)] int stars) => ModelState.IsValid ? View() : View("Signup");
}
```

The view shows the messages with `<errors />`:

```
<errors />                     @* every message *@
<errors for="Email" />         @* one field's messages *@
<input name="Name" label="Name: " />
```

Shown again this way, the form keeps the user's valid answers: each field without an error starts from what was
posted, so Enter keeps it, and only the fields with errors are asked afresh. Masked fields (passwords) are never filled
in again.

## Rules

- Validation runs only when form data is posted; navigating to an action without a form never produces errors.
- A value that cannot be converted gives one message (`The value 'twelve' is not valid for Your age.`, using
  `[Display(Name)]`) and that field is not validated again.
- `IValidatableObject` runs for rules across several fields, once every attribute passes and no value failed to
  convert, so it never compares fallback values.
- `[Required]` checks reference and nullable types. A non-nullable `int` always has a value, so use `int?` with
  `[Required]` to demand one.
- Error keys are the form field names (case-insensitive), so they match `<input name="…">` and `<errors for="…">`.
- `TryValidateModel(model)` validates an object built in code and returns whether it is valid; calling it again does
  not repeat messages.
- `ModelState` is passed to the view that the action returns, and to its partials. It does not survive a
  `RedirectToAction`.

| API | Purpose |
|---|---|
| `ModelState.IsValid` | `true` when binding and validation produced no errors |
| `ModelState.ErrorCount` | Total number of messages |
| `ModelState.AddModelError(key, message)` | Add your own error (`""` for one that belongs to no field) |
| `ModelState.ErrorsFor(key)` | One field's messages |
| `ModelState.Clear()` | Remove every error |
