# Design: ConsoleMVC 1.31: Mouse support

| | |
|---|---|
| **Author** | Mateusz Podeszwa |
| **Status** | Implemented in 1.31.0 |
| **Scope** | `MvcApplicationBuilder.Mouse`, `MouseDecoder`, `ClickTarget`, `IMarkupRenderer.MapClicks`, `KeysAt` and `SetMouseReporting`, `LineEditingInput`, `ScriptedInput.Click` and `Wheel` |

---

## 1. Problem

Terminals can report mouse clicks, and users expect to click a menu option or a choice rather than type its key.

## 2. Design

A click is turned into **the keys that would have the same effect**, so every element keeps working through its normal
key handling, and shortcuts, line editing and tests need nothing new.

```mermaid
sequenceDiagram
    participant T as Terminal
    participant D as MouseDecoder (innermost)
    participant S as ShortcutInput / LineEditingInput
    participant E as Element (menu, choice, checkbox)
    participant R as MarkupRenderer
    E->>R: Render(this), MapClicks(targets)       (line → keys)
    T->>D: ESC [ < 0 ; 3 ; 9 M                   (one key at a time)
    D->>R: KeysAt(row 8)
    R-->>D: Clear, "2", Enter
    D-->>S: Clear, "2", Enter
    S-->>E: the line "2"
```

```
MouseDecoder.ReadKey():
    pending keys first
    key ← inner.ReadKey(); not Escape → key
    Escape: read "[<b;x;yM" while each next key arrives within 25 ms; anything else → Escape, then the keys read, as typed
    report: left press → renderer.KeysAt(y − 1) queued (nothing when the row has no target)
            wheel up/down (64/65) → Up/Down arrow;  releases and other buttons → nothing

MarkupRenderer rows:  every line written +1; RecordInput (Enter after a prompt) +1; erasing n lines −n; Clear → 0
    MapClicks(targets): row of the last element drawn + target.Line → keys        (rows drawn over are forgotten)
    KeysAt(row):        row + rows scrolled out of the window (rows written + 1 − height, at least 0)

Targets:
    <menu> option (every line of it)  → Clear, its key, Enter           (Clear erases what was typed)
    <select>, <radio> choice          → Home, Down × position, Enter    (Space for <select multiple>)
    <checkbox>                        → Space
    <scroll>, lists                   → the wheel's arrows
```

- `builder.Mouse = true` (or the `ConsoleMVC:Mouse` setting) installs the decoder and reads lines key by key
  (`LineEditingInput`, which `ShortcutInput` now extends), and turns on SGR mouse reporting (`ESC[?1000h ESC[?1006h`)
  for the run through `TerminalModeSession`, which also puts it back after an error or Ctrl+C.
- `LineEditor` understands `ConsoleKey.Clear` (erase the line), which typing never produces and shortcuts refuse.
- Horizontal `<radio>` fields have no click targets; text fields are answered by typing.
- Targets are forgotten when the screen is cleared or redrawn after a resize, and set again when the element next draws.
