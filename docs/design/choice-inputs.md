# Design: ConsoleMVC 1.15: Choice inputs

| | |
|---|---|
| **Author** | Mateusz Podeszwa |
| **Status** | Implemented in 1.15.0 |
| **Scope** | `<select>`, `<radio>`, `<choice>`, `<checkbox>`, arrow-key selection, collection binding |

---

## 1. Goal

Ask for one of a few known values, several of them, or a yes/no answer without making the user type it, and bind the
answer to a model like any other form field.

```
<select name="Colour" label="Favourite colour:" default="green">
    <choice value="red">Red</choice>
    <choice value="green">Green</choice>
    <choice value="blue">Blue</choice>
</select>

<select name="Toppings" label="Toppings:" multiple>
    @foreach (var topping in Model.Toppings)
    {
        <choice value="@topping.Id">@topping.Name</choice>
    }
</select>

<radio name="Size" label="Size:" default="M">
    <choice>S</choice><choice>M</choice><choice>L</choice>
</radio>

<checkbox name="Newsletter" label="Send me the newsletter" />
```

```csharp
public class OrderForm
{
    public string Colour { get; set; } = "";
    public List<int> Toppings { get; set; } = [];
    public Size Size { get; set; }           // enums bind from the choice value
    public bool Newsletter { get; set; }     // "true" or "false"
}
```

## 2. Two ways to answer

| Mode | When | Keys |
|---|---|---|
| **Arrow keys** | the input is a terminal (`ITextInput.CanReadKeys`) and the renderer can rewrite its last element in place (`IMarkupRenderer.CanRewrite`, ANSI terminals) | Up/Down (Left/Right for `<radio>`) move, Space ticks, Enter confirms, 1 to 9 jump |
| **Typed** | anything else: piped input, tests, legacy consoles | a number, several numbers (`1,3`), the value or the label; empty takes the default |

Both modes give the same form value, so tests written with scripted lines cover the same rules.

```
Arrow keys                                 Typed
Favourite colour: (Up/Down, Enter)         Favourite colour:
 > Red                                       1) Red
   Green                                     2) Green
   Blue                                      3) Blue
                                           Choose 1-3 [Green]: 3
after Enter:
Favourite colour: Blue
```

## 3. Classes (UML)

```
            «interface»                      «interface»
            IFormField                       ITextInput
  + Name : string                  + ReadLine() : string?
  + Read(renderer, input) : string?  + ReadMasked(mask) : string?
            ▲                        + CanReadKeys : bool          (default false)
            │                        + ReadKey() : ConsoleKeyInfo?  (default null)
  ┌─────────┼──────────────┬───────────────────┐
InputElement  ChoiceFieldElement (abstract)    CheckboxElement
              + Label, Default, Required       + Label, Checked, Required
              + LabelColour, HighlightColour
              + Choices : IReadOnlyList<ChoiceElement>
              + Add(ChoiceElement)
              # LayoutChoices(width, state)  (abstract)
              # Horizontal : bool            (abstract)
                 ▲               ▲
           SelectElement     RadioElement
           + Multiple

ChoiceElement : InlineElement      (<choice>; Value defaults to the label text)

«interface» IMarkupRenderer
  + CanRewrite : bool              (default false)
  + Rewrite(element)               (default Render(element)): replaces the output of the last Render
«interface» IRenderTarget
  + TryEraseLines(count) : bool    (default false; ANSI: ESC[nF ESC[J)
```

`ConsoleViewBase.ReadInput(IFormField)` stores the answer in `Form[field.Name]`, so generated code is the same
`this.ReadInput(element);` for every field.

## 4. Rules (pseudocode)

```
Read(field):
    no choices → InvalidOperationException (a <select> built from an empty list)
    multiple and a value contains ',' → InvalidOperationException (the answer is comma-separated)
    if input.CanReadKeys and renderer.CanRewrite → ReadWithKeys else ReadTyped

ReadTyped:
    render label and numbered choices; loop:
        prompt "Choose 1-N [default]: "; line ← ReadLine; null → return null
        empty → Default (or "" ; required → error, ask again)
        tokens ← multiple ? split(line, ',') : [line]
        each token: number 1..N, else a value, else a label (case-insensitive) → choice; unknown → error, ask again
        return values joined with ','

ReadWithKeys:
    cursor ← index of Default (first one for multiple), ticked ← Default values
    Render(this); loop:
        key ← ReadKey; null → return null
        Up/Left → cursor-1, Down/Right → cursor+1 (wrapping); digit d → cursor = d-1
        single: the highlighted choice is the selection
        Space (multiple) → toggle ticked[cursor]
        Enter: multiple, required and nothing ticked → show error; else answered ← true
        Rewrite(this)                          (in place; a resize redraw replays the latest state)
    answered: the element lays out as "Label: answer"

Checkbox typed: "Label? [y/N]: " y/yes/true/1 or n/no/false/0; empty → Checked; required and not ticked → error
Checkbox keys:  "[ ] Label" Space toggles, y/n set, Enter confirms; answer "true" or "false"

Binding: T[], List<T>, IList<T>, ICollection<T>, IEnumerable<T>, IReadOnlyList<T>, IReadOnlyCollection<T> of a simple
    T split the value on ',', trim each part, drop empty parts and convert each; a bad part is a conversion error
```

## 5. Rewriting in place

`MarkupRenderer` remembers how many lines the last element took. `Rewrite` erases exactly those lines and writes the
new layout, and replaces the element's entry in the screen history, so a resize redraw shows the latest state. When
it cannot (a prompt or foreign output came after the element, the target has no cursor control, or the element is
taller than the terminal), it redraws the whole screen from history, or renders again when that is not possible.

## 6. Generator

| Element | Content | Placement | Attributes |
|---|---|---|---|
| `<select>` | `<choice>` (and C# such as `@foreach`) | top level | `name` (required), `label`, `default`, `required`, `multiple`, `color`, `highlight-color` |
| `<radio>` | `<choice>` | top level | as `<select>` without `multiple` |
| `<choice>` | inline text | inside `<select>` or `<radio>` | `value` and the inline styles |
| `<checkbox>` | none | top level | `name` (required), `label`, `checked`, `required`, `color` |

`ElementSchema` accepts several required parents so `<choice>` can live in both lists.
