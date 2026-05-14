# Customizing Keybindings and Themes

All keyboard shortcuts in the editor are remappable, and the colour theme can be changed or overridden, through Terminal.Gui's Configuration Manager. Settings are stored in a JSON file that is read at startup.

## Configuration file location

The configuration file is named `config.json`. Terminal.Gui searches for it in the following locations (most-specific first):

1. The current working directory
2. The user's Terminal.Gui config folder (e.g. `~/.tui/` on Linux/macOS, `%APPDATA%\tui\` on Windows)
3. The application's directory

The first file found for each setting wins, so a project-local `config.json` overrides the user-wide one.

## Remapping keyboard shortcuts

Each editor action is identified by a `Command` name. To override a binding, set the `Terminal.Gui.Editor.Editor.DefaultKeyBindings` key in your `config.json`:

```json
{
  "$schema": "https://gui-cs.github.io/Terminal.Gui/schemas/tui-config-schema.json",
  "Terminal.Gui.Editor.Editor.DefaultKeyBindings": {
    "Cut":  { "All": ["Ctrl+W"] },
    "Copy": { "All": ["Ctrl+Shift+C"] },
    "Undo": { "All": ["Ctrl+Z"], "Macos": ["Cmd+Z"] }
  }
}
```

Each entry maps a `Command` name to a `PlatformKeyBinding` object with the following optional keys:

| Key | Platform |
|---|---|
| `All` | Applied on every platform |
| `Windows` | Applied on Windows only (in addition to `All`) |
| `Linux` | Applied on Linux only (in addition to `All`) |
| `Macos` | Applied on macOS only (in addition to `All`) |

Keys are expressed as strings in the form `"Key"`, `"Ctrl+Key"`, `"Shift+Key"`, `"Ctrl+Shift+Key"`, etc. (e.g. `"Ctrl+Z"`, `"F3"`, `"Shift+F3"`).

### Common commands and their default keys

The most commonly remapped commands and their default keys:

| Command | Default key(s) |
|---|---|
| `Cut` | `Ctrl+X` |
| `Copy` | `Ctrl+C` |
| `Paste` | `Ctrl+V` |
| `Undo` | `Ctrl+Z` |
| `Redo` | `Ctrl+Y`, `Ctrl+Shift+Z` |
| `SelectAll` | `Ctrl+A` |
| `DeleteCharLeft` | `Backspace` |
| `DeleteCharRight` | `Delete` |
| `NewLine` | `Enter` |
| `Start` | `Ctrl+Home` |
| `End` | `Ctrl+End` |
| `Collapse` | `Ctrl+M` |

## Selecting a built-in theme

Set the `"Theme"` key to the name of a built-in theme:

```json
{
  "$schema": "https://gui-cs.github.io/Terminal.Gui/schemas/tui-config-schema.json",
  "Theme": "Default"
}
```

Terminal.Gui ships with a `"Default"` theme. The application may provide additional named themes.

## Overriding theme colours

You can override individual colour scheme values within a theme by adding them under `"Themes"`. Each scheme entry has `Normal`, `Focus`, `HotNormal`, `HotFocus`, and `Disabled` slots, each with `Foreground` and `Background` colours.

The following example overrides the `Base` colour scheme in the `Default` theme to use a green-on-black normal attribute and white-on-cyan focus attribute:

```json
{
  "$schema": "https://gui-cs.github.io/Terminal.Gui/schemas/tui-config-schema.json",
  "Theme": "Default",
  "Themes": {
    "Default": {
      "Schemes": [
        {
          "Base": {
            "Normal":    { "Foreground": "BrightGreen", "Background": "Black" },
            "Focus":     { "Foreground": "White",       "Background": "Cyan"  },
            "HotNormal": { "Foreground": "Yellow",      "Background": "Black" },
            "HotFocus":  { "Foreground": "Blue",        "Background": "Cyan"  },
            "Disabled":  { "Foreground": "DarkGray",    "Background": "Black" }
          }
        }
      ]
    }
  }
}
```

Valid colour names are the standard 16-colour terminal names: `Black`, `DarkRed`, `DarkGreen`, `DarkYellow`, `DarkBlue`, `DarkMagenta`, `DarkCyan`, `Gray`, `DarkGray`, `Red`, `Green`, `Yellow`, `Blue`, `Magenta`, `Cyan`, `White`, and `BrightWhite`. 24-bit hex colours (`"#RRGGBB"`) are also accepted on terminals that support them.

## Further reading

- [Terminal.Gui Configuration documentation](https://gui-cs.github.io/Terminal.Gui/docs/config)
- [Keyboard Reference](keyboard-reference.md) — full list of default shortcuts
