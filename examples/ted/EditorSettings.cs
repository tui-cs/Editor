using System.Text.Json;
using System.Text.Json.Nodes;
using Terminal.Gui.App;
using Terminal.Gui.Configuration;

namespace Ted;

/// <summary>
///     ted's persisted editor settings. These are real Terminal.Gui configuration properties
///     (<see cref="ConfigurationPropertyAttribute" />, <see cref="AppSettingsScope" />), so
///     <see cref="ConfigurationManager" /> is the single authority for <b>reading</b> them: enabling
///     CM (see <c>Program.cs</c>) loads <c>~/.tui/ted.config.json</c> and applies the values to these
///     static properties. ted does no parsing of its own.
///     <para>
///         <see cref="Save(string)" /> is hand-rolled only because Terminal.Gui exposes no API for
///         writing a user config file. It emits the exact shape CM reads: app-defined
///         (<see cref="AppSettingsScope" />) properties live nested under a top-level
///         <c>"AppSettings"</c> object, keyed <c>DeclaringType.PropertyName</c>. Other top-level keys
///         a user may have added (e.g. <c>"Theme"</c>) are preserved; JSONC comments are not.
///         Any legacy flat root-level <c>"EditorSettings.*"</c> keys from the pre-CM format are
///         dropped on save (migration).
///     </para>
/// </summary>
internal static class EditorSettings
{
    [ConfigurationProperty (Scope = typeof (AppSettingsScope))]
    public static bool LineNumbers { get; set; } = true;

    [ConfigurationProperty (Scope = typeof (AppSettingsScope))]
    public static bool FoldIndicators { get; set; } = true;

    [ConfigurationProperty (Scope = typeof (AppSettingsScope))]
    public static bool WordWrap { get; set; }

    [ConfigurationProperty (Scope = typeof (AppSettingsScope))]
    public static bool ShowTabs { get; set; }

    [ConfigurationProperty (Scope = typeof (AppSettingsScope))]
    public static int IndentSize { get; set; } = 4;

    [ConfigurationProperty (Scope = typeof (AppSettingsScope))]
    public static bool ConvertTabsToSpaces { get; set; } = true;

    [ConfigurationProperty (Scope = typeof (AppSettingsScope))]
    public static bool AutoIndent { get; set; } = true;

    [ConfigurationProperty (Scope = typeof (AppSettingsScope))]
    public static bool Scrollbars { get; set; } = true;

    [ConfigurationProperty (Scope = typeof (AppSettingsScope))]
    public static bool AutoComplete { get; set; }

    internal static void Save (string path)
    {
        try
        {
            JsonObject root = ReadRoot (path);

            // Migration: drop legacy flat root-level "EditorSettings.*" keys (the pre-CM
            // hand-rolled format). CM reads ted's settings only from the "AppSettings" object.
            foreach (var legacyKey in root
                         .Where (kvp => kvp.Key.StartsWith ("EditorSettings.", StringComparison.Ordinal))
                         .Select (kvp => kvp.Key)
                         .ToList ())
            {
                root.Remove (legacyKey);
            }

            JsonObject appSettings;

            if (root["AppSettings"] is JsonObject existing)
            {
                appSettings = existing;
            }
            else
            {
                appSettings = new JsonObject ();
                root["AppSettings"] = appSettings;
            }

            appSettings["EditorSettings.LineNumbers"] = LineNumbers;
            appSettings["EditorSettings.FoldIndicators"] = FoldIndicators;
            appSettings["EditorSettings.WordWrap"] = WordWrap;
            appSettings["EditorSettings.ShowTabs"] = ShowTabs;
            appSettings["EditorSettings.IndentSize"] = IndentSize;
            appSettings["EditorSettings.ConvertTabsToSpaces"] = ConvertTabsToSpaces;
            appSettings["EditorSettings.AutoIndent"] = AutoIndent;
            appSettings["EditorSettings.AutoComplete"] = AutoComplete;
            appSettings["EditorSettings.Scrollbars"] = Scrollbars;

            var directory = Path.GetDirectoryName (path);

            if (!string.IsNullOrWhiteSpace (directory))
            {
                Directory.CreateDirectory (directory);
            }

            File.WriteAllText (path, root.ToJsonString (new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception ex)
        {
            Logging.Error ($"EditorSettings.Save: {ex.GetType ().Name}: {ex.Message}");
        }
    }

    internal static string GetConfigPath ()
    {
        var home =
            Environment.GetEnvironmentVariable ("HOME")
            ?? Environment.GetFolderPath (Environment.SpecialFolder.UserProfile);

        return Path.Combine (home, ".tui", "ted.config.json");
    }

    private static JsonObject ReadRoot (string path)
    {
        if (!File.Exists (path))
        {
            return new JsonObject ();
        }

        var text = File.ReadAllText (path);

        if (string.IsNullOrWhiteSpace (text))
        {
            return new JsonObject ();
        }

        // Tolerate the JSONC TG itself accepts (// comments, trailing commas).
        JsonNode? node = JsonNode.Parse (
            text,
            documentOptions: new JsonDocumentOptions
            {
                CommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true
            });

        return node as JsonObject ?? new JsonObject ();
    }
}
