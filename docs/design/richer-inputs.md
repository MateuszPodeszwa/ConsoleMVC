# Design: ConsoleMVC 1.20: Number and date fields, filtered lists

| | |
|---|---|
| **Author** | Mateusz Podeszwa |
| **Status** | Implemented in 1.20.0 |
| **Scope** | `<number>`, `<date>`, `<select filter size>` |

---

## 1. Goal

Ask for numbers and dates without hand-written parsing and range checks, and let the user find one entry in a long
list by typing part of it.

```
<number name="Qty" label="Quantity:" min="1" max="99" decimals="0" default="1" />
<date name="Due" label="Due:" min="today" max="today+90" default="today+7" />
<select name="Country" label="Country:" filter size="8">
    @foreach (var country in Model.Countries) { <choice value="@country.Code">@country.Name</choice> }
</select>
```

## 2. Classes (UML)

```
«interface» IFormField
      ▲
      ├── NumberElement     Min, Max : decimal?   Step : decimal = 1   Decimals : int?   Default : string?
      │                     Validate(text, out decimal) : string?      (the message, or null)
      ├── DateElement       Min, Max, Default : string? ("2026-10-27", "today", "today+N")   Clock : TimeProvider
      │                     static TryParse(text, today, out DateOnly) : bool
      └── ChoiceFieldElement
            # AllowsFilter : bool  (SelectElement.Filter)
            # VisibleRows  : int   (SelectElement.Size)
```

## 3. Rules (pseudocode)

```
<number>
    parse: invariant culture, optional sign and decimal point (no thousands separators)
    errors: not a number → "Enter a number, e.g. 42 or 2.5."; decimals=0 and a fraction → "Enter a whole number."
            more places than decimals → "…at most N decimal places."; out of range → "Enter a number from A to B."
    form value: invariant, trailing zeros removed ("2.50" → "2.5")
    keys: Up/Down ± step, PageUp/PageDown ± 10 steps (clamped to the range); digits, '-', '.' edit, the first one
          replaces the starting value; Backspace; Esc restores the default; Enter validates (error shown in place)

<date>
    accepts: yyyy-MM-dd, today, tomorrow, yesterday, today±N, ±N (days)
    min/max/default use the same forms, checked at build time (CVW101) and resolved when the field is shown
    form value: yyyy-MM-dd
    keys: Left/Right pick year, month or day; Up/Down change it (month and year changes keep the day in the month);
          PageUp/PageDown by a week, a year or ten years; t = today; always clamped to the range; Enter confirms

<select filter>
    keys: printable characters build a filter (contains, in label or value, ignoring case); Backspace and Esc edit it;
          the highlight jumps to the first match; Enter with no match shows "No choice matches '…'."
    typed: a number, value or label as before; otherwise part of a name that matches exactly one choice
<select size="N">
    keys: shows N choices around the highlight with "(k more above)" / "(k more below)"; PageUp/PageDown move N
```
