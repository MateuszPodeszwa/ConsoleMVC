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
`@expression` shows as a placeholder such as `{Model.Title}`. Partial views and the layout are read from `Views/Shared`
(and the area's), and nothing else is read. The markup's diagnostics are written to standard error, or listed after
the screen in HTML.
