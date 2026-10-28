# Design: ConsoleMVC 1.34: Editor support

| | |
|---|---|
| **Author** | Mateusz Podeszwa |
| **Status** | Implemented in 1.34.0 |
| **Scope** | `schema/cvw-schema.json` (`MarkupSchemaExport`, `MarkupDocumentation`), the `ConsoleMVC.Preview` tool (`cvw-preview`), and the editor plugins in [ConsoleMVC-CVWSupport](https://github.com/MateuszPodeszwa/ConsoleMVC-CVWSupport) |

---

## 1. Problem

Editors treat `.cvw` files as plain text, or, in the Rider plugin, as the plain C# of ConsoleMVC 1.x. Writing markup
means remembering every element, attribute and value, and seeing a screen means building and running the application.

## 2. Goals

- Highlighting, completion, hover documentation and early checks for C-View Markup in VS Code and Rider.
- A live preview of a view while it is edited, drawn by the real rendering engine.
- One description of the markup, owned by the framework: editors must never fall out of step with the generator.

## 3. Structure

```mermaid
flowchart LR
    S[MarkupSchema<br/>generator] --> E[MarkupSchemaExport]
    D[MarkupDocumentation] --> E
    E --> J[(schema/cvw-schema.json)]
    J --> V[VS Code extension]
    J --> R[Rider plugin]
    P[ConsoleMVC.Preview<br/>cvw-preview] --> V
    P --> R
    G[CvwDocumentParser<br/>generator] --> P
    X[ConsoleMVC.Rendering] --> P
```

### 3.1 The schema file

`MarkupSchemaExport.ToJson(MarkupSchema.Default)` writes the generator's own schema, so the file cannot describe an
element the generator does not know. A generator test compares it with the checked-in `schema/cvw-schema.json` and
fails when they differ; `UPDATE_SCHEMA=1 dotnet test` rewrites it. Another test checks that every value the schema
offers for completion passes the generator's validation.

```text
{ formatVersion: 1,
  directives:  [ { name, syntax, description } ],            @model @using @inject @layout @refresh
  constructs:  [ { syntax, description } ],                  @expr @(…) @{…} @if @foreach @*…*@ @@ &lt;
  elements:    [ { name, description, content: void|inline|block|elements, topLevelOnly,
                   parents: [..] | null, children: [..] | null,
                   attributes: [ { name, description, required, kind, accepts, flag, interpolation,
                                   values?, minimum?, maximum? } ] } ],
  diagnostics: [ { id, severity, title } ] }
```

`kind` is one of `text`, `integer`, `number`, `number-list`, `date`, `boolean`, `colour`, `length`, `choice`, `route`
and `model`. Descriptions live in `MarkupDocumentation`, keyed by the attribute's runtime property so aliases share one
text, with `tag.Property` overrides; the export fails when one is missing, so a new element cannot ship undescribed.
Fields are only added; a change of meaning raises `formatVersion`.

### 3.2 The preview tool

```mermaid
sequenceDiagram
    participant Ed as Editor
    participant T as cvw-preview
    participant P as CvwDocumentParser
    participant B as PreviewBuilder
    participant R as MarkupRenderer + HtmlRenderTarget
    Ed->>T: stdin: the unsaved text, --file Views/Home/IndexView.cvw --width 80 --format html
    T->>P: Parse(text, MarkupSchema.Default)
    P-->>T: CvwDocument (nodes, diagnostics)
    T->>B: Build(document, project files)
    B-->>T: elements (layout around the view, partials inlined)
    T->>R: Render each element at the width
    R-->>Ed: HTML (or text, or ANSI) on stdout
```

```text
Build(nodes):
    code nodes (@if, @foreach, @{ })     → skipped: no C# runs, every element is drawn once
    element node                          → new runtime element (the schema's runtime type, created by reflection)
        class="…"                         → theme.Apply(element, classes)
        literal attribute                 → checked by the attribute's AttributeType, converted to the property's type
                                            (string, bool, int, decimal, double, Colour, Length, number list, an
                                            enum member or a predefined value such as BorderStyle.Double)
        attribute with @expressions       → text properties get "{Model.Name}"; other kinds keep their default,
                                            number lists get a sample series and a numeric value="@…" a sample
                                            number (30, 50, 20, …), so charts and bars are visible
        inline content                    → AppendText, with "{expression}" for each @expression; <br /> → AppendLineBreak
        block content                     → children; runs of text become an implicit <text> (as the emitter does)
    <partial name="X" />                  → Views/Shared/X.cvw (or the area's Shared folder), built in place (depth ≤ 8)
    the view                              → drawn inside @layout's (or _Layout's) layout at its <body />
Render: Theme.CreateDefault() (or high contrast), MarkupRenderer at --width, one element after another
Output: html  → <pre> with a <span style> per run of one style, colours from ConsolePalette
        text  → plain lines;  ansi → AnsiRenderTarget
        diagnostics → a list after the screen (html) or on stderr, with line and column
```

- **Interactive elements** draw their prompt, as the first frame the user would see: `<menu>` its options and prompt,
  `<input>` its label, `<select>` its choices, `<scroll>` all of its content.
- **Safety**: the tool runs no user code and loads no assembly from the project. It reads only the view, the files in
  `Views/Shared` (and the area's) and nothing outside the project's `Views` folders; partial and layout names are
  checked to be plain identifiers before they become paths.
- The tool is a `dotnet tool` (`dotnet tool install -g ConsoleMVC.Preview`), released with the framework at the same
  version. It references the generator's parser through `InternalsVisibleTo`, so it parses exactly as the build does.

### 3.3 The editors

The plugins live in ConsoleMVC-CVWSupport and read the schema file: the VS Code extension (TextMate grammar,
completion, hover, diagnostics, snippets and a preview panel that runs `cvw-preview` on every change, debounced), and
the Rider plugin (lexer, highlighting, completion, annotator and a preview tool window). They are released separately.

## 4. Testing

- `SchemaExportTests`: the checked-in file is current, valid JSON, describes everything, and offers only valid values.
- `PreviewTests`: each kind of attribute value, inline and block content, implicit text, expressions as placeholders,
  code skipped, partials and layouts resolved (and refused outside the project), interactive elements, the HTML output's
  colours and escaping, diagnostics, and the command line.
