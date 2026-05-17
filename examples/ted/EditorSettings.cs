using System.Text.RegularExpressions;
using Terminal.Gui.App;
using Terminal.Gui.Configuration;

namespace Ted;

internal sealed class TedSettingsScope;

internal static class EditorSettings
{
    [ConfigurationProperty (Scope = typeof (TedSettingsScope))]
    public static bool LineNumbers { get; set; } = true;

    [ConfigurationProperty (Scope = typeof (TedSettingsScope))]
    public static bool FoldIndicators { get; set; } = true;

    [ConfigurationProperty (Scope = typeof (TedSettingsScope))]
    public static bool WordWrap { get; set; }

    [ConfigurationProperty (Scope = typeof (TedSettingsScope))]
    public static bool ShowTabs { get; set; }

    [ConfigurationProperty (Scope = typeof (TedSettingsScope))]
    public static int IndentSize { get; set; } = 4;

    [ConfigurationProperty (Scope = typeof (TedSettingsScope))]
    public static bool ConvertTabsToSpaces { get; set; } = true;

    [ConfigurationProperty (Scope = typeof (TedSettingsScope))]
    public static bool AutoIndent { get; set; } = true;

    /// <summary>
    ///     Loads settings from the config file at <see cref="GetConfigPath" />.
    ///     Called once at startup before constructing <see cref="TedApp" />.
    /// </summary>
    internal static void Load ()
    {
        Load (GetConfigPath ());
    }

    internal static void Load (string path)
    {
        if (!File.Exists (path))
        {
            return;
        }

        try
        {
            string text = File.ReadAllText (path);

            LineNumbers = ReadBool (text, "EditorSettings.LineNumbers", LineNumbers);
            FoldIndicators = ReadBool (text, "EditorSettings.FoldIndicators", FoldIndicators);
            WordWrap = ReadBool (text, "EditorSettings.WordWrap", WordWrap);
            ShowTabs = ReadBool (text, "EditorSettings.ShowTabs", ShowTabs);
            IndentSize = ReadInt (text, "EditorSettings.IndentSize", IndentSize);
            ConvertTabsToSpaces = ReadBool (text, "EditorSettings.ConvertTabsToSpaces", ConvertTabsToSpaces);
            AutoIndent = ReadBool (text, "EditorSettings.AutoIndent", AutoIndent);
        }
        catch (Exception ex)
        {
            Logging.Error ($"EditorSettings.Load: {ex.GetType ().Name}: {ex.Message}");
        }
    }

    internal static void Save ()
    {
        Save (GetConfigPath ());
    }

    internal static void Save (string path)
    {
        try
        {
            EnsureConfigFile (path);
            string text = File.ReadAllText (path);
            Dictionary<string, string> entries = new ()
            {
                ["EditorSettings.LineNumbers"] = ToJson (LineNumbers),
                ["EditorSettings.FoldIndicators"] = ToJson (FoldIndicators),
                ["EditorSettings.WordWrap"] = ToJson (WordWrap),
                ["EditorSettings.ShowTabs"] = ToJson (ShowTabs),
                ["EditorSettings.IndentSize"] = IndentSize.ToString (),
                ["EditorSettings.ConvertTabsToSpaces"] = ToJson (ConvertTabsToSpaces),
                ["EditorSettings.AutoIndent"] = ToJson (AutoIndent)
            };

            List<string> toInsert = [];

            foreach ((string key, string value) in entries)
            {
                Regex pattern = new (
                    $@"^(?<prefix>\s*""{Regex.Escape (key)}""\s*:\s*)(?:true|false|-?\d+)(?<suffix>\s*,?\s*(?://.*)?)$",
                    RegexOptions.Multiline);
                bool replaced = false;
                text = pattern.Replace (
                    text,
                    match =>
                    {
                        replaced = true;

                        return $"{match.Groups["prefix"].Value}{value}{match.Groups["suffix"].Value}";
                    },
                    1);

                if (replaced)
                {
                    continue;
                }

                toInsert.Add ($"  \"{key}\": {value}");
            }

            if (toInsert.Count > 0)
            {
                int lastBrace = FindRootClosingBrace (text);

                if (lastBrace >= 0)
                {
                    int insertCommaAfter = FindLastObjectMemberCharacterPosition (text, lastBrace);

                    if (insertCommaAfter >= 0 && text[insertCommaAfter] != ',' && text[insertCommaAfter] != '{')
                    {
                        text = text.Insert (insertCommaAfter + 1, ",");
                        lastBrace = FindRootClosingBrace (text);
                    }

                    string insertion = $"\n\n{string.Join (",\n", toInsert)}\n";
                    text = text.Insert (lastBrace, insertion);
                }
            }

            File.WriteAllText (path, text);
        }
        catch (Exception ex)
        {
            Logging.Error ($"EditorSettings.Save: {ex.GetType ().Name}: {ex.Message}");
        }
    }

    internal static string GetConfigPath ()
    {
        string home =
            Environment.GetEnvironmentVariable ("HOME")
            ?? Environment.GetFolderPath (Environment.SpecialFolder.UserProfile)
            ?? Directory.GetCurrentDirectory ();

        return Path.Combine (home, ".tui", "ted.config.json");
    }

    private static void EnsureConfigFile (string path)
    {
        string? directory = Path.GetDirectoryName (path);

        if (!string.IsNullOrWhiteSpace (directory))
        {
            Directory.CreateDirectory (directory);
        }

        if (!File.Exists (path))
        {
            File.WriteAllText (path, "{}");
        }
    }

    private static string ToJson (bool value)
    {
        return value ? "true" : "false";
    }

    private static bool ReadBool (string json, string key, bool defaultValue)
    {
        // Match key only at a JSON property position: line starts with optional whitespace,
        // then the key. Negative lookahead skips // comment lines.
        Match m = Regex.Match (
            json,
            $@"^(?!\s*//)\s*""{Regex.Escape (key)}""\s*:\s*(?<v>true|false)",
            RegexOptions.IgnoreCase | RegexOptions.Multiline);

        return m.Success ? string.Equals (m.Groups["v"].Value, "true", StringComparison.OrdinalIgnoreCase) : defaultValue;
    }

    private static int ReadInt (string json, string key, int defaultValue)
    {
        Match m = Regex.Match (
            json,
            $@"^(?!\s*//)\s*""{Regex.Escape (key)}""\s*:\s*(?<v>-?\d+)",
            RegexOptions.Multiline);

        return m.Success && int.TryParse (m.Groups["v"].Value, out int v) ? v : defaultValue;
    }

    /// <summary>
    ///     Finds the last '}' that is NOT inside a // comment.
    ///     Scans backwards, skipping any '}' on a line whose non-whitespace content starts with //.
    /// </summary>
    private static int FindRootClosingBrace (string text)
    {
        int i = text.Length - 1;

        while (i >= 0)
        {
            i = text.LastIndexOf ('}', i);

            if (i < 0)
            {
                return -1;
            }

            // Check if this '}' is on a comment line
            int lineStart = text.LastIndexOf ('\n', i) + 1;
            string lineBeforeBrace = text[lineStart..i];

            if (lineBeforeBrace.TrimStart ().StartsWith ("//", StringComparison.Ordinal))
            {
                i--;

                continue;
            }

            return i;
        }

        return -1;
    }

    private static int FindLastObjectMemberCharacterPosition (string text, int braceIndex)
    {
        int i = braceIndex - 1;

        while (i >= 0)
        {
            char c = text[i];

            if (char.IsWhiteSpace (c))
            {
                i--;

                continue;
            }

            int lineStart = text.LastIndexOf ('\n', i) + 1;
            string line = text[lineStart..(i + 1)];
            string trimmedLine = line.TrimStart ();

            if (trimmedLine.StartsWith ("//", StringComparison.Ordinal))
            {
                i = lineStart - 1;

                continue;
            }

            int commentStart = line.IndexOf ("//", StringComparison.Ordinal);

            if (commentStart >= 0)
            {
                string withoutComment = line[..commentStart];
                int lastNonWhitespace = withoutComment.TrimEnd ().Length - 1;

                if (lastNonWhitespace >= 0)
                {
                    return lineStart + lastNonWhitespace;
                }

                i = lineStart - 1;

                continue;
            }

            return i;
        }

        return -1;
    }
}
