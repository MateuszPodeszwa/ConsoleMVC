# Design: ConsoleMVC 1.17: Compile-time route checks

| | |
|---|---|
| **Author** | Mateusz Podeszwa |
| **Status** | Implemented in 1.17.0 |
| **Scope** | CVW119 for literal `nav` targets that name a missing controller or action |

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
