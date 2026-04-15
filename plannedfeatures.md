# Planned Features & Project Direction

## Vision

The goal of ConsoleMVC is to make writing console applications **easy, simple, and fun**.

Console applications deserve the same architectural respect as web applications. A console window isn't that different from a web page — think of it as a **2D version of a 3D web page**. On a web page, you can jump between sections, click links, scroll freely. In a console, everything flows top to bottom, one line after another. But the core principle is the same: **each screen has one responsibility** — collect input and render output.

That's a SOLID foundation (pun intended). The Single Responsibility Principle applies beautifully here: a console "page" should do one thing well. Collect what it needs, show what it needs, and hand control to the next page. That's exactly what ConsoleMVC already does with Controllers, Views, and Models.

But we can take this much further.

---

## The Big Idea: C-View Markup (Console View Markup)

Right now, `.cvw` files are plain C# code. You write `Console.WriteLine()` calls, manually format text, and handle all the layout yourself. It works, but it's not *fun*. Imagine instead:

```
@model MyApp.Models.DashboardViewModel

<box title="Dashboard" width="60" border="double">
    <text align="center" color="cyan">Welcome, @Model.UserName!</text>
    <separator />
    <table>
        <row>
            <cell width="30">Notifications</cell>
            <cell>@Model.NotificationCount</cell>
        </row>
        <row>
            <cell width="30">Last login</cell>
            <cell>@Model.LastLogin.ToString("g")</cell>
        </row>
    </table>
</box>

<br />

<menu prompt="Select an option:">
    <option key="1" nav="Home/Index">Home</option>
    <option key="2" nav="Settings/Index">Settings</option>
    <option key="q" quit="true">Exit</option>
</menu>
```

**C-View Markup** would be a markup language purpose-built for console UIs. Not HTML — something new, designed specifically for the constraints and strengths of terminal output.

### Key ideas:

- **HTML-like syntax** with tags like `<box>`, `<table>`, `<text>`, `<menu>`, `<input>` — but semantically meaningful for consoles, not browsers
- **`@` expressions** borrowed from Razor — `@Model.Name`, `@if`, `@foreach` — for embedding C# logic directly in the markup
- **Style attributes** like `width`, `color`, `align`, `border` — controlling how elements render in the terminal
- **Built-in form elements** — `<input>`, `<select>`, `<menu>` — that handle user input collection and automatically generate form data for posting to controllers
- **Layout primitives** — boxes, separators, tables, grids — making it trivial to create structured console output that would normally require dozens of `Console.WriteLine()` calls

### What it is NOT:

- It is **not HTML**. Browsers won't render it. It's a domain-specific markup for terminals only.
- It is **not a wrapper around an existing library**. While libraries like Spectre.Console do impressive work, I want to build something from the ground up that's tightly integrated with the MVC framework and the source generator pipeline. Something that feels native, not bolted on.
- It is **not a templating engine**. It compiles down to real C# code at build time, just like current `.cvw` files. The source generator would parse the markup and emit optimised rendering code.

### How it would work:

1. You write `.cvw` files using the new markup syntax (plain C# would still be supported for backwards compatibility)
2. The source generator parses the markup at build time
3. It emits a `ConsoleView<TModel>` subclass with a `Render()` method that contains the compiled rendering logic
4. At runtime, the rendering engine draws boxes, tables, and text using ANSI escape sequences or platform-specific console APIs

---

## Roadmap (Rough Order)

### Near-term
- [ ] Dependency injection support — register and resolve services in controllers
- [ ] Middleware pipeline — before/after action hooks for cross-cutting concerns
- [ ] Async action support — `Task<ActionResult>` return types
- [ ] Form validation framework — attributes like `[Required]`, `[MaxLength]` on form models, with automatic validation before the action executes

### Medium-term
- [ ] C-View Markup v0.1 — basic tags (`<text>`, `<box>`, `<separator>`, `<br>`)
- [ ] Console rendering engine — ANSI-based rendering with colour, alignment, and borders
- [ ] `<table>` support — data-driven tables with column widths and alignment
- [ ] `<menu>` and `<input>` tags — declarative input collection that auto-generates form data

### Long-term
- [ ] C-View Markup v1.0 — full markup language with layout system, theming, and responsive terminal sizing
- [ ] IDE plugin support for markup — syntax highlighting, completion, and preview for C-View Markup in Rider and VS Code
- [ ] Component system — reusable markup components (`<partial name="Header" />`)
- [ ] Live reload — detect `.cvw` changes and hot-reload the running console app during development

---

## Get Involved

I'm a student building this project because I believe console apps deserve better tooling. There's a lot I don't know, and I'd genuinely love input from experienced developers who've built frameworks, rendering engines, or markup parsers before.

**Here's how you can help:**

- **Open an issue** on [GitHub](https://github.com/MateuszPodeszwa/ConsoleMVC/issues) — share ideas, point out things I'm missing, suggest a better approach. I read every issue.
- **Reach out by email** — if you have thoughts that don't fit in an issue, or just want to chat about the project direction, feel free to email me.
- **Join the Patreon** (free tier) — even as a free member, you get a voice in which features get prioritised. This isn't about money, it's about building a community around the project.
- **Contribute** — PRs are welcome. Check out [CONTRIBUTING.md](.github/CONTRIBUTING.md) for guidelines.

Whether you're a seasoned framework author or another student who thinks this is cool — I want to hear from you. The best ideas come from collaboration, and I'd rather build something great with the community than something mediocre alone.
