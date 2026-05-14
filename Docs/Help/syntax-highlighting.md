# Syntax Highlighting

The editor supports language-aware syntax highlighting that colours keywords, strings, comments, and other tokens according to the active language definition and colour theme.

## Supported languages

The following languages are available out of the box:

- C#
- C / C++
- Java
- JavaScript
- Python
- PowerShell
- T-SQL
- Visual Basic
- JSON
- HTML
- XML
- CSS
- Markdown

## Changing the language

In `ted`, click the language name shown in the status bar to open a language picker, or use the **Language** menu item. The editor recolours immediately.

If you open a file, `ted` selects the language automatically based on the file extension.

## Colour themes

Syntax highlight colours follow the active Terminal.Gui colour scheme. Changing the theme updates the editor colours live.

### Choosing a built-in theme

Set the `"Theme"` key in `config.json`:

```json
{
  "$schema": "https://gui-cs.github.io/Terminal.Gui/schemas/tui-config-schema.json",
  "Theme": "Default"
}
```

### Overriding theme colours for the editor

To change how highlighted text looks, override the colour scheme values for the active theme. The following example changes the `Base` scheme to use bright-green text on a black background:

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

The **Use Theme Background** option controls whether highlighted text uses the theme's background colour or the editor's own background. See [Customizing Keybindings and Themes](configuration.md) for the full list of valid colour names and the config file format.

## Notes

- Highlighting is applied through a pluggable `IVisualLineTransformer` pipeline. Host applications can add their own transformers for custom colouring on top of (or instead of) the built-in highlighter.
- TextMate grammar support (`.tmLanguage` / `.tmGrammar`) is planned for a future release.
