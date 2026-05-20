# Recording Agent — ted & Terminal.Gui TUIcast Guide

This document teaches an AI agent how to compose TUIcast keystroke scripts for
recording the `ted` example app (a Terminal.Gui `Editor` demo). Any AI system
(Claude, Copilot, Codex, etc.) can use this as context to drive `record-ted.ps1`.

## Quick start

```powershell
./scripts/record-ted.ps1 `
    -Name "search-replace" `
    -Title "ted: find and replace" `
    -Keystrokes "wait:2000,Ctrl+H,hello,Tab,world,Alt+A,wait:1500,Esc"
```

---

## TUIcast keystroke syntax

A keystroke script is a **comma-separated** string. Each token is one of:

| Token type | Examples | Description |
|---|---|---|
| **Wait** | `wait:2000` | Pause N milliseconds before next key |
| **Named key** | `Enter`, `Escape`, `Tab`, `Space`, `Backspace`, `Delete` | Single special key press |
| **Arrow/nav** | `CursorUp`, `CursorDown`, `CursorLeft`, `CursorRight`, `Home`, `End`, `PageUp`, `PageDown` | Navigation keys |
| **Function key** | `F1`–`F12` | Function keys |
| **Modifier combo** | `Ctrl+S`, `Ctrl+Shift+Z`, `Alt+A`, `Shift+Tab` | Modifier + key |
| **Literal text** | `hello world` | Typed character-by-character (spaces included) |

### Rules

- Tokens are **case-sensitive** for modifiers: `Ctrl`, `Alt`, `Shift`.
- Named keys are case-insensitive but conventional: `CursorDown` not `cursordown`.
- Literal text tokens are everything that doesn't match a known key name or `wait:N`.
- Commas inside literal text are **not supported** — split around them with separate tokens.
- Use `Esc` or `Escape` for the escape key.
- `wait:N` is essential between actions that trigger UI transitions (dialog open,
  file load, menu animation). **Always wait after opening a dialog or menu.**

---

## ted UI structure

### Window layout

```
┌─ ted — Terminal.Gui.Editor demo ─────────────────────────────────────┐
│ [MenuBar] _File  _Edit  _View  _Options  _Help    <filename>         │
│ [Editor - full document editing area with gutter]                     │
│                                                                       │
│                                                                       │
│ [StatusBar] Language | Theme | INS | Ln 1, Col 1                     │
└──────────────────────────────────────────────────────────────────────┘
```

### Menu bar (activated by F9 or Alt+<letter>)

**File** (`Alt+F` or `F9` then first item):
- **New** — `Ctrl+N`
- **Open** — `Ctrl+O`
- **Save** — `Ctrl+S`
- **Save As** — `Ctrl+Shift+S`
- **Quit** — `Ctrl+Q`

**Edit** (`Alt+E`):
- **Find...** — `Ctrl+F`
- **Replace...** — `Ctrl+H`
- ─── separator ───
- **Undo** — `Ctrl+Z`
- **Redo** — `Ctrl+Y`
- ─── separator ───
- **Cut** — `Ctrl+X`
- **Copy** — `Ctrl+C`
- **Paste** — `Ctrl+V`
- **Select All** — `Ctrl+A`

**View** (`Alt+V`):
- Line Numbers (checkbox)
- Fold Indicators (checkbox)
- Word Wrap (checkbox)
- Show Tabs (checkbox)
- Scrollbars (checkbox)
- Preview Markdown

**Options** (`Alt+O`):
- Settings...

**Help** (`Alt+H`):
- About

### Keyboard shortcuts (in Editor)

| Action | Key |
|---|---|
| Move caret | Arrow keys, Home, End, PageUp, PageDown |
| Select | Shift + any movement key |
| Select all | Ctrl+A |
| Delete line | Ctrl+Shift+K |
| Undo | Ctrl+Z |
| Redo | Ctrl+Y |
| Cut | Ctrl+X |
| Copy | Ctrl+C |
| Paste | Ctrl+V |
| Find | Ctrl+F |
| Replace | Ctrl+H |
| Toggle overwrite | Insert |
| Fold/unfold | Click gutter or use fold indicators |

### Open dialog

Triggered by `Ctrl+O`. A standard Terminal.Gui `OpenDialog`:
- Has a text field for the path (pre-focused)
- Type a relative or absolute path and press `Enter`
- Or navigate the file list with arrows and `Enter`
- `Esc` cancels

### Find/Replace dialog

Triggered by `Ctrl+F` (Find tab) or `Ctrl+H` (Replace tab):

```
┌─ Find / Replace ──────────────────────────────────┐
│ [x] Match case  [ ] Whole word  [ ] Regex         │
│ Status:                                            │
│ ┌─ Find ─────────────────────────────────────────┐│
│ │ Find:    [________________]                    ││
│ │                                                ││
│ │ [Find Next] [Find Previous]                    ││
│ └────────────────────────────────────────────────┘│
│ ┌─ Replace ──────────────────────────────────────┐│
│ │ Find:    [________________]                    ││
│ │ Replace: [________________]                    ││
│ │                                                ││
│ │ [Find Next] [Replace] [Replace All]            ││
│ └────────────────────────────────────────────────┘│
│                                       [ Close ]   │
└───────────────────────────────────────────────────┘
```

- **Ctrl+F** opens with the Find tab active; cursor in Find field
- **Ctrl+H** opens with the Replace tab active; cursor in Find field
- **Tab** moves between fields and buttons
- **Alt+N** = Find Next, **Alt+P** = Find Previous
- **Alt+R** = Replace, **Alt+A** = Replace All
- **Alt+C** = toggle Match Case, **Alt+W** = toggle Whole Word, **Alt+X** = toggle Regex
- **Esc** or **Alt+L** (Close button) dismisses the dialog

### Settings dialog

Triggered by Options → Settings. Has checkboxes for:
- Convert tabs to spaces
- Auto-indent
- Word wrap
- Show tabs
- Auto-complete
- Indent size (numeric)

---

## Composing keystroke scripts — best practices

1. **Always start with a wait** — `wait:1500` or `wait:2000` gives the app time to
   start and render its first frame.

2. **Wait after UI transitions** — opening a dialog, switching tabs, or loading a file
   needs `wait:500` to `wait:1000` for the UI to settle before the next action.

3. **End with quit or Esc** — recordings should end cleanly. Use `Ctrl+Q` to quit
   (may prompt for save), or `Esc` to close a dialog.

4. **Keep recordings short** — aim for 10–30 seconds of real time. Viewers lose
   interest after that. Use `--MaxDuration 60` as a safety net.

5. **Show, don't rush** — generous waits between meaningful actions let the viewer
   see what happened. `wait:1500` after a find highlights the match visually.

6. **Open a file first** for most demos — unless you're demoing the empty-buffer
   experience, start by opening a file so there's content to work with.

---

## Example keystroke scripts

### Open a file and scroll

```
wait:2000,Ctrl+O,wait:500,./examples/ted/TedApp.cs,Enter,wait:2000,PageDown,wait:1500,PageDown,wait:1500,Ctrl+Q
```

### Find text

```
wait:2000,Ctrl+O,wait:500,./examples/ted/TedApp.cs,Enter,wait:1500,Ctrl+F,wait:500,Editor,Alt+N,wait:1000,Alt+N,wait:1000,Esc,wait:500,Ctrl+Q
```

### Find and replace

```
wait:2000,Ctrl+O,wait:500,./examples/ted/TedApp.cs,Enter,wait:1500,Ctrl+H,wait:500,Editor,Tab,View,Alt+A,wait:1500,Esc,wait:500,Ctrl+Z,wait:1000,Ctrl+Q
```

### Toggle view options

```
wait:2000,Ctrl+O,wait:500,./README.md,Enter,wait:1500,Alt+V,wait:300,CursorDown,CursorDown,Enter,wait:1000,Alt+V,wait:300,CursorDown,CursorDown,CursorDown,Enter,wait:1500,Ctrl+Q
```

### Type and undo

```
wait:2000,Hello world!,wait:1000,Enter,This is ted.,wait:1500,Ctrl+Z,wait:500,Ctrl+Z,wait:500,Ctrl+Z,wait:1500,Ctrl+Q
```

---

## Invoking the script

```powershell
# Minimal — just keystrokes
./scripts/record-ted.ps1 -Keystrokes "wait:2000,Ctrl+O,wait:500,./README.md,Enter,wait:2000,Ctrl+Q"

# Full options
./scripts/record-ted.ps1 `
    -Name "find-replace" `
    -Title "ted: search and replace demo" `
    -Keystrokes "wait:2000,Ctrl+O,wait:500,./examples/ted/TedApp.cs,Enter,wait:1500,Ctrl+H,wait:500,Editor,Tab,View,Alt+A,wait:1500,Esc,wait:500,Ctrl+Q" `
    -Cols 120 `
    -Rows 36 `
    -MaxDuration 45
```

### Parameters

| Parameter | Required | Default | Description |
|---|---|---|---|
| `-Keystrokes` | **Yes** | — | The TUIcast keystroke script |
| `-Name` | No | `demo` | Short ID for filenames (`ted-<Name>.gif`) |
| `-Title` | No | `ted demo` | Title in cast metadata |
| `-Output` | No | `artifacts/tuicast/ted-<Name>.gif` | GIF path |
| `-CastOutput` | No | `artifacts/tuicast/ted-<Name>.cast` | Cast path |
| `-Cols` | No | 120 | Terminal columns |
| `-Rows` | No | 36 | Terminal rows |
| `-MaxDuration` | No | 60 | Safety timeout (seconds) |
| `-DrainMs` | No | 1500 | Wait after last keystroke |
| `-SkipBuild` | No | false | Skip `dotnet build` |
| `-TuicastVersion` | No | `0.1.1` | Auto-download version |

---

## For AI agents — how to use this

When asked to "record ted doing X", follow this process:

1. **Read this document** for keystroke syntax and ted's UI layout.
2. **Plan the interaction** — break the demo into steps (open file → navigate →
   perform action → show result → close).
3. **Compose the keystroke string** — use waits generously between transitions.
4. **Call the script** with appropriate `-Name` and `-Title`.
5. **Report the output paths** back to the user.

You do NOT need to know the exact pixel layout — TUIcast drives the app through
its terminal input, just like a user would type. Focus on the logical key
sequence to accomplish the demo goal.
