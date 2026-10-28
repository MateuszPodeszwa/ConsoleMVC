# Native AOT and Trimming

A ConsoleMVC application can be published as a single native executable that starts almost instantly and needs no
.NET installation:

```bash
dotnet publish -c Release -r linux-x64 -p:PublishAot=true
./bin/Release/net10.0/linux-x64/publish/MyApp
```

Use `win-x64`, `osx-arm64` or another runtime identifier for other systems. The template's first screen appears in
about 18 ms as Native AOT, against about 280 ms with the JIT, from a 7 MB executable.

## What makes it work

The source generator writes a **route table** for your project: every controller with a typed factory, every action
with a typed call, every view, and the model types that model binding fills. The router uses it instead of reflection,
so the trimmer keeps exactly what the application uses. Nothing needs to be configured.

`dotnet publish -p:PublishTrimmed=true` (a trimmed, framework-independent build without native compilation) works the
same way.

## Settings

The template sets this in its project file, so options bound from `appsettings.json` use generated code too:

```xml
<EnableConfigurationBindingGenerator>true</EnableConfigurationBindingGenerator>
```

## What to keep in mind

| Write | Instead of |
|---|---|
| `RedirectToAction("Details", new { id })` | passing route values typed as `object`, whose properties trimming cannot see |
| `@inject IClock Clock` in a `.cvw` view | `[Inject]` properties in hand-written view classes |
| one public constructor in a controller | several constructors (they work, but are created by reflection) |

The build tells you: publishing reports a warning (IL2026 and friends) for anything that may not survive trimming.
Hot reload (`dotnet watch`) is not used in published applications; while developing, the application finds new
controllers and views after an update by scanning, as before.
