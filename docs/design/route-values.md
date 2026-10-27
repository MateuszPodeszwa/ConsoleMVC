# Design: ConsoleMVC 1.13: Route values in navigation

| | |
|---|---|
| **Author** | Mateusz Podeszwa |
| **Status** | Implemented in 1.13.0 |
| **Scope** | `nav="Orders/Details?id=42"`, `NavigationResult.To(target)`, `RedirectToAction(action, routeValues)`, `RouteValues` |

---

## 1. Goal

Open a record from a list without building a form: the menu option, the view's C# or a redirect carries the values
the next action needs, like a query string.

```
@foreach (var order in Model.Orders)
{
    <option key="@order.Number" nav="Orders/Details?id=@order.Id">@order.Title</option>
}
```

```csharp
public ActionResult Details(int id) => View(store.Find(id));
public ActionResult Save(OrderForm form) => RedirectToAction("Details", new { id = form.Id });
```

## 2. Rules (pseudocode)

```
ParseTarget("Controller/Action?key=value&…") → (controller | null, action, values | null)
    path must be 'Controller/Action' or 'Action'; keys and values are URL-decoded; empty pairs are skipped

Generator: nav="Item?name=@name" → "Item?name=" + Uri.EscapeDataString(Format(name))
    expressions after '?' are escaped, so a value with '&' or '=' cannot split the values
    literal nav values are checked at build time (CVW101), including '?id=42'

RouteValues.From(object): dictionaries are copied; other objects give their public properties;
    values use the invariant culture (1.5, 2026-10-27, true); null values are left out

Binding: values ← route values, then form data on top (form data wins)
Validation: only when form data is posted (route values identify, forms submit); conversion errors are still reported
History: a screen remembers its route values, so Back returns to the same record
```
