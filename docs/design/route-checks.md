# Design: ConsoleMVC 1.17: Compile-time route checks

| | |
|---|---|
| **Author** | Mateusz Podeszwa |
| **Status** | Implemented in 1.17.0 (markup) and 1.21.0 (C# and the code fix) |
| **Scope** | CVW119 for `nav` targets, CVW120 for navigation in C#, CVW121 and a code fix for missing views |

---

## 1. Goal

A typo in a menu is found when you build, not when a user picks the option:

```
<option key="2" nav="Orders/Detials">Order details</option>
```

```
Views/Home/IndexView.cvw(8,26): warning CVW119: nav="Orders/Detials": the controller 'Orders' has no action
'Detials'. Actions: Details, Index, List.
```

## 2. Pipeline

```
C# syntax ──ClassDeclaration "*Controller"──▶ ControllerEntry(Name, Actions)   (per class, value-equal)
                                                  │ Collect
                                                  ▼
                                            RouteCatalogue ──────┐
.cvw files ──parse──▶ CvwDocument ──────────────────────────────┼──▶ RouteChecker.Check ──▶ CVW119 warnings
                                                                 │
                                    (views are still generated; the check never blocks the build by itself)
```

Only controller and action names feed the catalogue, so editing the body of an action, or any other C#, keeps the
cached views.

## 3. Rules (pseudocode)

```
controller(type):  non-abstract, non-generic class named *Controller deriving from ConsoleMVC.Mvc.Controller
actions(type):     public instance ordinary methods on the type and its bases below Controller, returning
                   ActionResult, Task<ActionResult> or ValueTask<ActionResult> (or a derived result type),
                   not [NonAction]                                       (the same rules as the runtime Router)

check(view, nav):
    nav has an @expression           → skip (only known at run time)
    catalogue is empty               → skip (controllers compiled elsewhere)
    path ← nav before '?'            (route values are not checked)
    "Action":  view in Views/Shared, or its folder has no controller → skip; else controller ← folder
    "Controller/Action": controller missing → CVW119 listing the controllers
                         action missing     → CVW119 listing that controller's actions
    names match case-insensitively, as at run time
```

## 4. Severity

CVW119 is a warning: an application can load controllers from another assembly
(`MvcApplicationBuilder.ApplicationAssembly`), which the generator cannot see. Projects that want a typo to fail the
build raise it in `.editorconfig`:

```ini
[*.cvw]
dotnet_diagnostic.CVW119.severity = error
```

## 5. Navigation in C# (1.21)

`NavigationAnalyzer`, a `DiagnosticAnalyzer` in the generator assembly, applies the same catalogue to C#. It sees the
compilation after the generator ran, so views from `.cvw` files count, and it analyses the generated view code, so a
call in a view is reported at the `.cvw` line.

```
on each invocation with constant string arguments (literals, const fields, nameof):
    in a controller:
        RedirectToAction(action [, controller] [, routeValues])  → controller defaults to the current one   → CVW120
        View("Name" [, model])                                     → needs Views/{Controller}/{Name}View    → CVW121
        View([model]) directly inside an action                     → needs the action's view               → CVW121
    anywhere (views: the controller is the view's folder):
        NavigationResult.To(controller, action [, …]), To("Controller/Action?…"), To("Action"), ToAction(action) → CVW120
    skipped: computed names; View() in a helper method; compilations without controllers
```

CVW121 carries the controller, the view name and, when the call passes a typed model, the model's type. The code fix
`CreateViewCodeFix` ("Create view Views/Orders/DetailsView.cvw") lives in `ConsoleMVC.CodeFixes`, a separate assembly
because code fixes need the Roslyn workspace, which compilers do not load; both assemblies ship in the framework
package's `analyzers` folder. It writes a view with that `@model`, a heading and a menu with Back, and only accepts
identifiers as folder and file names.
