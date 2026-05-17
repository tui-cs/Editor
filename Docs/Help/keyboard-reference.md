# Keyboard Reference

This page lists all default keyboard shortcuts. All shortcuts are `Command`-bound and can be remapped by the host application via Terminal.Gui's `KeyBindings` API or a Configuration Manager profile.

## Navigation

| Key | Action |
|---|---|
| `←` | Move caret left one character |
| `→` | Move caret right one character |
| `↑` | Move caret up one line |
| `↓` | Move caret down one line |
| `Home` | Move caret to start of line |
| `End` | Move caret to end of line |
| `Ctrl+Home` | Move caret to start of document |
| `Ctrl+End` | Move caret to end of document |
| `Page Up` | Move caret up one page |
| `Page Down` | Move caret down one page |

## Selection

| Key | Action |
|---|---|
| `Shift+←` | Extend selection left one character |
| `Shift+→` | Extend selection right one character |
| `Shift+↑` | Extend selection up one line |
| `Shift+↓` | Extend selection down one line |
| `Shift+Home` | Extend selection to start of line |
| `Shift+End` | Extend selection to end of line |
| `Shift+Ctrl+Home` | Extend selection to start of document |
| `Shift+Ctrl+End` | Extend selection to end of document |
| `Shift+Page Up` | Extend selection up one page |
| `Shift+Page Down` | Extend selection down one page |
| `Ctrl+A` | Select all |

## Editing

| Key | Action |
|---|---|
| Any printable character | Insert character (or replace selection) |
| `Enter` | Insert newline (and auto-indent if strategy is active) |
| `Backspace` | Delete character to the left (or delete selection; indentation-aware) |
| `Delete` | Delete character to the right (or delete selection) |
| `Tab` | Indent line / indent selected lines |
| `Shift+Tab` | Un-indent line / un-indent selected lines |

## Multi-Caret

| Key | Action |
|---|---|
| `Ctrl+Click` | Toggle an additional caret at the clicked position |
| `Ctrl+Alt+↑` | Add a caret on the line above (at the sticky column) — VS Code parity |
| `Ctrl+Alt+↓` | Add a caret on the line below (at the sticky column) — VS Code parity |
| `Alt`+drag | Build a vertical column of carets from the press row to the drag row (carets only) |
| `Tab` / `Shift+Tab` | Indent / un-indent at every caret (one undo step) |
| `Esc` | Collapse back to the primary caret |

`Ctrl+Alt+↑/↓` is configurable per platform via `Terminal.Gui.Editor.Editor.DefaultKeyBindings`
(see *Remapping shortcuts* below) — there is no separate built-in fallback chord for terminals/WMs
that grab `Ctrl+Alt+arrow`. The column-drag uses `Alt`+drag (not VS Code's `Shift+Alt`): inside
a terminal, `Shift`+drag is the terminal's own forced text-selection and `Alt` makes it a
rectangular block, so `Shift+Alt`+drag would never reach the editor. The mouse modifier is not
yet user-configurable — tracked by [gui-cs/Terminal.Gui#4888](https://github.com/gui-cs/Terminal.Gui/issues/4888).

## Clipboard

| Key | Action |
|---|---|
| `Ctrl+C` | Copy selection to clipboard |
| `Ctrl+X` | Cut selection to clipboard |
| `Ctrl+V` | Paste from clipboard |

## History

| Key | Action |
|---|---|
| `Ctrl+Z` | Undo |
| `Ctrl+Y` | Redo |
| `Ctrl+Shift+Z` | Redo (alternative) |

## Find & Replace

| Key | Action |
|---|---|
| `Ctrl+F` | Open Find (raises `FindRequested` event) |
| `Ctrl+H` | Open Replace (raises `ReplaceRequested` event) |
| `F3` | Find next |
| `Shift+F3` | Find previous |

## Folding

| Key | Action |
|---|---|
| `Ctrl+M` | Toggle fold under caret |

## Scrolling

| Input | Action |
|---|---|
| Mouse wheel up | Scroll up one line |
| Mouse wheel down | Scroll down one line |
| Mouse wheel left | Scroll left |
| Mouse wheel right | Scroll right |

## Remapping shortcuts

To change a shortcut, add the new binding to your `config.json`:

```json
{
  "$schema": "https://gui-cs.github.io/Terminal.Gui/schemas/tui-config-schema.json",
  "Terminal.Gui.Editor.Editor.DefaultKeyBindings": {
    "Cut":  { "All": ["Ctrl+W"] },
    "Redo": { "All": ["Ctrl+Y"], "Macos": ["Cmd+Shift+Z"] }
  }
}
```

See [Customizing Keybindings and Themes](configuration.md) for full details on the config file format and available colour theme options.
