# ConsoleMVC

ConsoleMVC is a .NET framework that brings the familiar Model-View-Controller pattern from ASP.NET Core MVC to console applications. Rather than building interactive console programmes with tangled input loops and ad-hoc screen rendering, ConsoleMVC lets you structure your application around controllers, actions, and strongly-typed views — the same concepts used in web development, adapted for the terminal.

The framework handles the core event loop for you: it resolves the appropriate controller, invokes the requested action, renders a view, and waits for navigation input before repeating the cycle. You focus on writing controllers that return results and views that present data, while ConsoleMVC takes care of routing, discovery, and lifecycle management.

ConsoleMVC also ships with a Roslyn source generator that compiles `.cvw` template files — a Razor-like syntax for console views — into strongly-typed C# classes at build time. This means you get compile-time safety for your views without any runtime template parsing overhead.

## Quick Links

- [Getting Started](getting-started.md) — install the framework and create your first console MVC application.
- [C-View Markup](c-view-markup.md) — describe screens with `<box>`, `<text>` and `<table>`, and collect input with `<input>` and `<menu>`.
- [Dependency Injection](dependency-injection.md) — register services and receive them in controllers and views (`@inject`).
- [Async Actions and Middleware](async-and-middleware.md) — `await` in actions and run code around every action with `app.Use`.
- [API Reference](../api/) — auto-generated reference documentation for all public types.
