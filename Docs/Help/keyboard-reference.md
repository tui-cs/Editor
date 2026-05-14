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

To change a shortcut in your own application:

```csharp
// Remove the default binding for Ctrl+X.
editor.KeyBindings.Remove (Key.X.WithCtrl);

// Bind Ctrl+W to Cut instead.
editor.KeyBindings.Add (Key.W.WithCtrl, Command.Cut);
```

Or ship a Terminal.Gui Configuration Manager JSON profile that performs the same remapping without code changes.
