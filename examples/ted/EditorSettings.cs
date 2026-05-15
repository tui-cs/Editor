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
    public static bool UseThemeBackground { get; set; }

    [ConfigurationProperty (Scope = typeof (TedSettingsScope))]
    public static int IndentSize { get; set; } = 4;

    [ConfigurationProperty (Scope = typeof (TedSettingsScope))]
    public static bool ConvertTabsToSpaces { get; set; } = true;

    [ConfigurationProperty (Scope = typeof (TedSettingsScope))]
    public static bool AutoIndent { get; set; }

    internal static void Save ()
    {
        Save (GetConfigPath ());
    }

    internal static void Save (string path)
    {
        EnsureConfigFile (path);

        try
        {
            string text = File.ReadAllText (path);
            Dictionary<string, string> entries = new ()
            {
                ["EditorSettings.LineNumbers"] = ToJson (LineNumbers),
                ["EditorSettings.FoldIndicators"] = ToJson (FoldIndicators),
                ["EditorSettings.WordWrap"] = ToJson (WordWrap),
                ["EditorSettings.ShowTabs"] = ToJson (ShowTabs),
                ["EditorSettings.UseThemeBackground"] = ToJson (UseThemeBackground),
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

                        return $"{match.Groups ["prefix"].Value}{value}{match.Groups ["suffix"].Value}";
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
                int lastBrace = text.LastIndexOf ('}');

                if (lastBrace >= 0)
                {
                    int insertCommaAfter = FindLastJsonTokenPosition (text, lastBrace);

                    if (insertCommaAfter >= 0 && text[insertCommaAfter] != ',' && text[insertCommaAfter] != '{')
                    {
                        text = text.Insert (insertCommaAfter + 1, ",");
                        lastBrace = text.LastIndexOf ('}');
                    }

                    string insertion = $"\n\n{string.Join (",\n", toInsert)}\n";
                    text = text.Insert (lastBrace, insertion);
                }
            }

            File.WriteAllText (path, text);

            if (ConfigurationManager.IsEnabled)
            {
                ConfigurationManager.Load (ConfigLocations.All);
                ConfigurationManager.Apply ();
            }
        }
        catch (Exception ex)
        {
            Logging.Error ($"EditorSettings.Save: {ex.GetType ().Name}: {ex.Message}");
        }
    }

    private static string GetConfigPath ()
    {
        string baseDirectory;

        if (OperatingSystem.IsWindows ())
        {
            string appData = Environment.GetFolderPath (Environment.SpecialFolder.ApplicationData);
            baseDirectory = string.IsNullOrWhiteSpace (appData)
                ? Path.Combine (Directory.GetCurrentDirectory (), ".tui")
                : Path.Combine (appData, "tui");
        }
        else
        {
            string home =
                Environment.GetEnvironmentVariable ("HOME")
                ?? Environment.GetFolderPath (Environment.SpecialFolder.UserProfile)
                ?? Directory.GetCurrentDirectory ();
            baseDirectory = Path.Combine (home, ".tui");
        }

        string appName = string.IsNullOrWhiteSpace (ConfigurationManager.AppName) ? "ted" : ConfigurationManager.AppName;

        return Path.Combine (baseDirectory, $"{appName}.config.json");
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

    private static int FindLastJsonTokenPosition (string text, int braceIndex)
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
            string line = text[lineStart..(i + 1)].TrimStart ();

            if (line.StartsWith ("//", StringComparison.Ordinal))
            {
                i = lineStart - 1;

                continue;
            }

            return i;
        }

        return -1;
    }
}
