# ConsoleMVC.Preview

`cvw-preview` draws a [ConsoleMVC](https://github.com/MateuszPodeszwa/ConsoleMVC) `.cvw` view without building or
running the application, using the framework's real rendering engine. The VS Code and Rider plugins use it for their
live preview, and it works on its own in a terminal.

```bash
dotnet tool install -g ConsoleMVC.Preview
cvw-preview Views/Home/IndexView.cvw --width 100
```

| Option | Meaning |
|---|---|
| `--width N` | The width in columns: a whole number from 20 to 500 (default 80); any other value is an error |
| `--format text\|ansi\|html` | Plain text (default), coloured text for a terminal, or an HTML fragment |
| `--stdin` | Reads the view's text from standard input; the file only says where it is |
| `--high-contrast` | Uses the high-contrast theme |

A value may also be joined to its option with `=`, as in `--width=100`. Wrong arguments exit with code 1 and say what
is wrong.

No C# runs: every element is drawn once (the body of a loop appears once, both branches of an `if` appear), and each
`@expression` shows as a placeholder such as `{Model.Title}`. Fields and menus look as a terminal first shows them.
Partial views and the layout are read from the project's `Views/Shared`, and nothing else is read.

The build's diagnostics for the view are written to standard error, or listed after the screen in HTML: the markup's
own, and the checks of partial views and layouts (CVW114 to CVW118, CVW103, CVW010) and of the file's name (CVW001 to
CVW003, CVW009). Checks that need the compiled project (CVW004, CVW005, CVW119 to CVW121, and C# errors) need a build.
