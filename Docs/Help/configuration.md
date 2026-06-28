# Customizing Keybindings and Themes

All keyboard shortcuts in the editor are remappable, and the colour theme can be changed or overridden, through Terminal.Gui's Configuration Manager. Settings are stored in a JSON file that is read at startup.

## Configuration file locations

The configuration file is named `config.json`. Terminal.Gui searches for it in several locations, from lowest priority (built-in defaults) to highest (runtime):

| Location | Path |
|---|---|
| Built-in defaults | Hard-coded in Terminal.Gui |
| Library resources | Embedded in the Terminal.Gui assembly |
| Global user config | `~/.tui/config.json` |
| Global project config | `./.tui/config.json` |
| App-specific user config | `~/.tui/<appname>.config.json` |
| App-specific project config | `./.tui/<appname>.config.json` |

`<appname>` is the name of the running application (for example, `~/.tui/ted.config.json` for an application named `ted`). Settings are merged from lowest to highest priority — a more-specific file always overrides a less-specific one for the same setting.

## Remapping keyboard shortcuts

Each editor action is identified by a `Command` name. To override a binding, set the `Terminal.Gui.Editor.Editor.DefaultKeyBindings` key in your `config.json`:

```json
{
  "$schema": "https://tui-cs.github.io/Terminal.Gui/schemas/tui-config-schema.json",
  "Terminal.Gui.Editor.Editor.DefaultKeyBindings": {
    "Cut":  { "All": ["Ctrl+W"] },
    "Copy": { "All": ["Ctrl+Shift+C"] },
    "Undo": { "All": ["Ctrl+Z"] }
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

Keys are expressed as strings in the form `"Key"`, `"Ctrl+Key"`, `"Shift+Key"`, `"Ctrl+Shift+Key"`, `"Alt+Key"`, etc. (e.g. `"Ctrl+Z"`, `"Alt+Z"`, `"F3"`, `"Shift+F3"`). Terminal.Gui supports the `Ctrl`, `Shift`, and `Alt` modifiers; there is no `Cmd` or `Meta` modifier.

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

Terminal.Gui ships with one built-in theme: **`Default`**. Set the `"Theme"` key in your `config.json` to activate it (this is also the value used if you omit the key):

```json
{
  "$schema": "https://tui-cs.github.io/Terminal.Gui/schemas/tui-config-schema.json",
  "Theme": "Default"
}
```

The host application may register additional named themes. Ask the application documentation for any extra theme names it provides.

## Overriding theme colours

You can override individual colour scheme values within a theme by adding them under `"Themes"`. Each scheme entry has `Normal`, `Focus`, `HotNormal`, `HotFocus`, and `Disabled` slots, each with `Foreground` and `Background` colours.

The following example overrides the `Base` colour scheme in the `Default` theme to use a green-on-black normal attribute and white-on-cyan focus attribute:

```json
{
  "$schema": "https://tui-cs.github.io/Terminal.Gui/schemas/tui-config-schema.json",
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

Terminal.Gui supports over 160 named colours — the full W3C/CSS colour set plus additional terminal-specific names:

- **Classic terminal names** (always map to the terminal's own 16-colour palette): `Black`, `DarkRed`, `DarkGreen`, `DarkYellow`, `DarkBlue`, `DarkMagenta`, `DarkCyan`, `Gray`, `DarkGray`, `BrightRed`, `BrightGreen`, `BrightYellow`, `BrightBlue`, `BrightMagenta`, `BrightCyan`, `White`
- **W3C/CSS colour names**: `Aqua`, `Coral`, `CornflowerBlue`, `Crimson`, `DodgerBlue`, `Firebrick`, `GreenYellow`, `HotPink`, `IndianRed`, `LimeGreen`, `MediumBlue`, `Navy`, `OrangeRed`, `Purple`, `RoyalBlue`, `SaddleBrown`, `Salmon`, `SkyBlue`, `SlateBlue`, `Teal`, `Tomato`, `Violet`, `YellowGreen`, and many more
- **TG-specific names**: `AmberPhosphor`, `BrightBlack`, `Charcoal`, `Ebony`, `FluorescentOrange`, `GreenPhosphor`, `GuppieGreen`, `Jet`, `Onyx`, `OuterSpace`, `RaisinBlack`

24-bit hex colours (`"#RRGGBB"`) are also accepted on terminals that support them.

## Further reading

- [Terminal.Gui Configuration documentation](https://tui-cs.github.io/Terminal.Gui/docs/config)
- [Keyboard Reference](keyboard-reference.md) — full list of default shortcuts
