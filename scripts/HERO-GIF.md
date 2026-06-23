# Hero GIF Recording Guide

Produces `docs/images/hero.gif` — a high-quality animated GIF demonstrating ted's key features.

## Prerequisites

- [tuirec](https://github.com/tui-cs/tuirec) v0.3.4+ on PATH (`go install github.com/tui-cs/tuirec/cmd/tuirec@latest`)
- .NET 10 SDK (for building ted)
- `agg` is auto-downloaded by tuirec on first use

## Build ted

```powershell
dotnet build examples/ted -c Debug --nologo
```

## Record

```powershell
$binary = "C:/Users/Tig/s/tui-cs/Editor/examples/ted/bin/Debug/net10.0/ted.exe"
$file = "./examples/ted/TedApp.cs"

# Keystroke script — demonstrates typing, scrolling, folding, search, selection+indent,
# undo, themes, settings (autocomplete enable), autocomplete popup, disable autocomplete,
# and About box.
# Pacing: --keystroke-delay 50 (very fast, snappy feel).
$ks = 'wait:300,`// This is a demo of ted, the Terminal.Gui.Editor example app`,Enter,wait:300,PageDown,wait:80,PageDown,wait:80,PageDown,wait:200,Home,wait:300,click:5:23,wait:400,click:5:23,wait:200,PageDown,wait:80,PageDown,wait:80,PageDown,wait:80,PageDown,wait:80,PageDown,wait:80,PageDown,wait:200,Ctrl+F,wait:200,`SaveView`,wait:200,Enter,wait:250,Esc,wait:200,PageDown,wait:80,PageDown,wait:200,click:8:14,wait:150,Shift+CursorDown,Shift+CursorDown,Shift+CursorDown,Shift+CursorDown,wait:200,Tab,wait:250,Ctrl+Z,Ctrl+Z,Ctrl+Z,Ctrl+Z,Ctrl+Z,wait:250,click:20:30,wait:200,CursorDown,CursorDown,CursorDown,Enter,wait:400,click:20:30,wait:200,CursorDown,CursorDown,Enter,wait:400,click:20:30,wait:200,CursorUp,Enter,wait:400,F9,wait:150,CursorRight,CursorRight,CursorRight,Enter,wait:200,Enter,wait:250,Space,wait:150,Enter,wait:250,`Save`,wait:200,Ctrl+Space,wait:400,Esc,wait:200,Ctrl+Z,Ctrl+Z,Ctrl+Z,Ctrl+Z,Ctrl+Z,Ctrl+Z,Ctrl+Z,Ctrl+Z,Ctrl+Z,Ctrl+Z,wait:250,F9,wait:150,CursorRight,CursorRight,CursorRight,Enter,wait:200,Enter,wait:250,Space,wait:150,Enter,wait:250,click:29:1,wait:200,Enter,wait:800,Esc,wait:200,Esc,wait:200,Tab,Tab,Enter'

tuirec record `
    --binary $binary `
    --args $file `
    --name "hero" `
    --show-command '$ ted ./examples/ted/TedApp.cs' `
    --keystrokes $ks `
    --startup-delay 2000 `
    --drain 2000 `
    --cols 120 `
    --rows 30 `
    --keystroke-delay 50 `
    --max-duration 60 `
    --cast-output ./artifacts/hero.cast `
    --verbosity high

# Copy to final location
Copy-Item ./artifacts/hero.gif ./docs/images/hero.gif -Force
```

## Demo sequence

| Time   | Feature               | Keys / Actions                                           |
|--------|-----------------------|----------------------------------------------------------|
| 0-2s   | File load + highlight | `$ ted ./examples/ted/TedApp.cs` typed, file opens       |
| 2-5s   | Type comment line     | Types `// This is a demo of ted, the Terminal.Gui.Editor example app` + Enter |
| 5-6s   | Scrolling             | PageDown×3 then Home (show file navigation)              |
| 6-7s   | Folding               | Click fold gutter at row 23 (collapse/expand class body) |
| 7-9s   | Scroll to mid-file    | 6× PageDown                                              |
| 9-11s  | Search                | Ctrl+F → type "SaveView" → Enter (find next) → Esc      |
| 11-13s | Selection + indent    | Click col 8 + Shift+Down×4 to select, Tab to indent      |
| 13-14s | Undo                  | Ctrl+Z×5 to revert indent                                |
| 14-17s | Theme switching       | Theme dropdown → Anders → Green Phosphor → Dark          |
| 17-19s | Settings dialog       | F9 → Options → Settings → enable autocomplete → OK      |
| 19-21s | Autocomplete          | Type "Save" → Ctrl+Space → popup shown → Esc            |
| 19-20s | Disable autocomplete  | F9 → Options → Settings → uncheck autocomplete → OK     |
| 20-22s | About box + Quit      | Click Help → Enter → shown ~1s → Esc → Esc (quit)       |

## Tuning tips

- **Fold target**: Line 22 (`{` of `TedApp : Window` class body) — always visible at row 23 in the initial 30-row view. Click col 5 for the fold gutter.
- **Theme dropdown**: Located at approximately col 20, row 30 (status bar, last row). Items listed top-to-bottom: Default, 8-Bit, Amber Phosphor, Anders, Dark, Green Phosphor, Light, TurboPascal 5.
  - After selecting Anders (3 Down from Default), reopening shows Anders highlighted. From Anders: 2 Down = Green Phosphor. From Green Phosphor: 1 Up = Dark.
- **Help menu position**: "Help" is at col 29 in the menu bar (row 1). Use `click:29:1` to open it. The only item is "About ted…"; press Enter to select.
- **Settings dialog**: Reached via F9 (menu bar focus) → 3× CursorRight (to Options) → Enter → Enter (Settings is the only item). First tab has "Auto Complete" checkbox. Space toggles, Enter confirms OK.
- **Quit key is Esc** (not Ctrl+Q). Esc closes the topmost overlay first (search bar, popup, dialog), then quits the app when nothing is open. If the document is dirty, a "Save changes?" dialog appears with buttons [Cancel, Don't Save, Save] (Save focused). Press Tab,Tab,Enter to select "Don't Save".
- **`--kitty-keyboard` does NOT work** for navigation keys (CursorDown, PageDown, etc.) with current Terminal.Gui. Do NOT use this flag. Standard VT100 sequences work.
- **Multi-caret via Ctrl+Alt+Down** is unreliable without kitty keyboard. Use Shift+Down selection + Tab for indent demos instead.
- **After search**: the viewport centers on the found text. Budget additional PageDown presses to reach content below the match.

## Troubleshooting

1. **Keys not registering**: Use `--verbosity high` and check the escape sequences in stderr output. Without `--kitty-keyboard`, navigation uses standard VT100 sequences (`\x1b[B` for CursorDown, `\x1b[6~` for PageDown) which Terminal.Gui recognizes.
2. **Wrong click targets**: Do a discovery recording first — short script with just `wait:2000,Ctrl+Q`, then examine the `.cast` file to find column positions of UI elements.
3. **Theme dropdown doesn't open**: Verify the col:row coordinates match the "Theme" shortcut in the status bar. Parse the `.cast` for the "Default" text position.
4. **About dialog not appearing**: Use `click:29:1` (Help menu at col 29, row 1) instead of F9+CursorRight×4. After complex keystroke sequences, F9 menu navigation can be unreliable.
5. **File stays dirty after undos**: The autocomplete popup may auto-insert text beyond what you typed. Use 10+ Ctrl+Z to be safe, and handle the "Save changes?" dialog at quit with Tab,Tab,Enter.
6. **Recording too long**: Reduce `wait:` values or lower `--keystroke-delay`. Current recording is ~22s at delay 50.
7. **GIF too large**: The `agg` renderer produces ~0.5-2 MB GIFs at 120×30. For smaller files, reduce terminal dimensions.
