# Planned Features & Project Direction

## Vision

The goal of ConsoleMVC is to make writing console applications **easy, simple, and fun**.

Console applications deserve the same architectural respect as web applications. A console window isn't that different from a web page, think of it as a **2D version of a 3D web page**. On a web page, you can jump between sections, click links, scroll freely. In a console, everything flows top to bottom, one line after another. But the core principle is the same: **each screen has one responsibility**: collect input and render output.

That's a SOLID foundation (pun intended). The Single Responsibility Principle applies beautifully here: a console "page" should do one thing well. Collect what it needs, show what it needs, and hand control to the next page. That's exactly what ConsoleMVC already does with Controllers, Views, and Models.

But we can take this much further.

---

## Where ConsoleMVC Is Today

The original big idea, **C-View Markup**, is real: `.cvw` views describe screens with `<box>`, `<text>`, `<table>`, `<columns>`, `<input>`, `<select>`, `<checkbox>` and `<menu>`, compile to plain C# at build time, and are checked by the compiler. Around it, ConsoleMVC now has:

- [C-View Markup 1.0](docs/guides/docs/c-view-markup.md): text, boxes, tables, column layouts that stack on narrow terminals, themes with `class="…"`, inputs, menus and validation messages
- [Partial views](docs/design/partial-views.md) and [layouts](docs/design/layout-views.md): reusable markup in `Views/Shared`, checked at build time
- [Keyboard shortcuts](docs/design/shortcuts.md): `app.MapKey(ConsoleKey.F1, "Help/Index", "Help")` on every screen, listed by `<shortcuts />`
- [Navigation history](docs/design/navigation-history.md): Back options and `<breadcrumbs />`; [route values](docs/design/route-values.md) such as `nav="Orders/Details?id=@order.Id"`
- [Responsive screens](docs/design/responsive-redraw.md) that redraw when the terminal is resized
- [Compile-time route checks](docs/design/route-checks.md): a typo in `nav`, `RedirectToAction`, `NavigationResult.To` or `View("…")` is a build warning (CVW119 to CVW121), with a fix that creates a missing view
- [Choice inputs](docs/design/choice-inputs.md): `<select>` (also `multiple`), `<radio>` and `<checkbox>`, answered with the arrow keys in a terminal and typed otherwise; [number and date fields and filtered lists](docs/design/richer-inputs.md)
- [Form validation](docs/design/form-validation.md) with DataAnnotations and `ModelState`
- [Dependency injection](docs/guides/docs/dependency-injection.md) with `builder.Services` and `@inject`
- [Configuration and logging](docs/guides/docs/hosting.md): `appsettings.json`, environment variables, the command line, options and `ILogger<T>`, with a [rolling file logger](docs/design/file-logging.md)
- [Async actions and middleware](docs/guides/docs/async-and-middleware.md)
- [Live reload](docs/guides/docs/getting-started.md#live-reload) with `dotnet watch`
- [Testing](docs/guides/docs/testing.md): the `ConsoleMVC.Testing` package with a test host, scripted lines and keys, and snapshots; `dotnet new consolemvc --tests` starts with a test project

The ideas below are what comes next. They are proposals, not promises: if one matters to you, say so in an issue.

---

## Roadmap (Rough Order)

### Near-term


### Medium-term


- [ ] **Full-screen mode**: use the alternate screen buffer, redraw when the terminal is resized, and scroll long content (`<scroll>`, a pager for long lists)
- [ ] **Live elements**: `<progress>` and `<spinner>` that update while an async action runs, and views that refresh on a timer
- [ ] **Charts**: `<bar-chart>` and `<sparkline>` for dashboards
- [ ] **`<markdown>`**: render Markdown (headings, lists, emphasis, code) inside a view
- [ ] **Localisation**: resource-based strings in views and validation messages, with culture-aware formatting where wanted
- [ ] **Accessibility**: a screen-reader friendly mode without box drawing, and a high-contrast theme

### Long-term

- [ ] **IDE support for markup**: syntax highlighting, completion and a live preview for C-View Markup in Rider and VS Code (the Rider plugin currently treats markup as plain text)
- [ ] **Native AOT and trimming**: a generated route table instead of reflection, for instant start-up and single-file tools
- [ ] **Remote sessions**: serve a ConsoleMVC application over SSH or a web terminal, one session per user
- [ ] **Areas and modules**: group large applications into areas, and load feature modules from other assemblies
- [ ] **Mouse support**: click menu options and buttons in terminals that report mouse events

---

## Get Involved

I'm a student building this project because I believe console apps deserve better tooling. There's a lot I don't know, and I'd genuinely love input from experienced developers who've built frameworks, rendering engines, or markup parsers before.

**Here's how you can help:**

- **Open an issue** on [GitHub](https://github.com/MateuszPodeszwa/ConsoleMVC/issues), share ideas, point out things I'm missing, suggest a better approach. I read every issue.
- **Reach out by email**: if you have thoughts that don't fit in an issue, or just want to chat about the project direction, feel free to email me.
- **Join the Patreon** (free tier), even as a free member, you get a voice in which features get prioritised. This isn't about money, it's about building a community around the project.
- **Contribute**: PRs are welcome. Check out [CONTRIBUTING.md](.github/CONTRIBUTING.md) for guidelines.

Whether you're a seasoned framework author or another student who thinks this is cool, I want to hear from you. The best ideas come from collaboration, and I'd rather build something great with the community than something mediocre alone.
