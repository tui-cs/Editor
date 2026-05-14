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

Syntax highlight colours follow the active Terminal.Gui colour scheme. Changing the theme in the application (via the **Options** menu or the Configuration Manager) updates the editor colours live.

The host application can set the **Use Theme Background** option to control whether highlighted text uses the theme's background colour or the editor's own background.

## Notes

- Highlighting is applied through a pluggable `IVisualLineTransformer` pipeline. Host applications can add their own transformers for custom colouring on top of (or instead of) the built-in highlighter.
- TextMate grammar support (`.tmLanguage` / `.tmGrammar`) is planned for a future release.
