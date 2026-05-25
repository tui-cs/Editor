using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Configuration;
using Terminal.Gui.App;
using Terminal.Gui.Configuration;

namespace Ted;

#pragma warning disable CS0618 // Keep legacy CM attributes until Terminal.Gui fully removes CM.

/// <summary>
///     ted's persisted editor settings. Microsoft.Extensions.Configuration is the primary read path:
///     startup loads <c>~/.tui/ted.config.json</c> and applies the values to these static properties
///     before <see cref="TedApp" /> is constructed. Legacy CM attributes are retained only so older
///     Terminal.Gui builds can still apply the previous <see cref="AppSettingsScope" /> format.
///     <para>
///         <see cref="Save(string)" /> writes the MEC-native shape:
///         <c>"EditorSettings": { "WordWrap": true }</c>. Other top-level keys a user may have added
///         are preserved; JSONC comments are not. Legacy flat root-level
///         <c>"EditorSettings.*"</c> keys and old CM <c>"AppSettings"</c> entries are dropped on save
///         once migrated.
///     </para>
/// </summary>
internal static class EditorSettings
{
    internal const string SectionName = "EditorSettings";

    [ConfigurationProperty (Scope = typeof (AppSettingsScope))]
    public static bool LineNumbers
    {
        get => Defaults.LineNumbers;
        set => Defaults.LineNumbers = value;
    }

    [ConfigurationProperty (Scope = typeof (AppSettingsScope))]
    public static bool FoldIndicators
    {
        get => Defaults.FoldIndicators;
        set => Defaults.FoldIndicators = value;
    }

    [ConfigurationProperty (Scope = typeof (AppSettingsScope))]
    public static bool WordWrap
    {
        get => Defaults.WordWrap;
        set => Defaults.WordWrap = value;
    }

    [ConfigurationProperty (Scope = typeof (AppSettingsScope))]
    public static bool ShowTabs
    {
        get => Defaults.ShowTabs;
        set => Defaults.ShowTabs = value;
    }

    [ConfigurationProperty (Scope = typeof (AppSettingsScope))]
    public static int IndentSize
    {
        get => Defaults.IndentSize;
        set => Defaults.IndentSize = value;
    }

    [ConfigurationProperty (Scope = typeof (AppSettingsScope))]
    public static bool ConvertTabsToSpaces
    {
        get => Defaults.ConvertTabsToSpaces;
        set => Defaults.ConvertTabsToSpaces = value;
    }

    [ConfigurationProperty (Scope = typeof (AppSettingsScope))]
    public static bool AutoIndent
    {
        get => Defaults.AutoIndent;
        set => Defaults.AutoIndent = value;
    }

    [ConfigurationProperty (Scope = typeof (AppSettingsScope))]
    public static bool Scrollbars
    {
        get => Defaults.Scrollbars;
        set => Defaults.Scrollbars = value;
    }

    [ConfigurationProperty (Scope = typeof (AppSettingsScope))]
    public static bool AutoComplete
    {
        get => Defaults.AutoComplete;
        set => Defaults.AutoComplete = value;
    }

    internal static EditorSettingsValues Defaults { get; set; } = new ();

    internal static IConfiguration BuildConfiguration (string path)
    {
        return new ConfigurationBuilder ()
            .AddJsonFile (path, true, false)
            .Build ();
    }

    internal static void Load (string path)
    {
        Apply (BuildConfiguration (path));
    }

    internal static void Apply (IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull (configuration);

        EditorSettingsValues settings = new ();
        ApplyLegacyDottedKeys (configuration, settings);
        ApplyLegacyDottedKeys (configuration.GetSection ("AppSettings"), settings);
        configuration.GetSection (SectionName).Bind (settings);
        Defaults = settings;
    }

    internal static void ResetDefaults ()
    {
        Defaults = new EditorSettingsValues ();
    }

    internal static void Save (string path)
    {
        try
        {
            JsonObject root = ReadRoot (path);

            RemoveLegacyDottedKeys (root);

            if (root["AppSettings"] is JsonObject appSettings)
            {
                RemoveLegacyDottedKeys (appSettings);

                if (appSettings.Count == 0)
                {
                    root.Remove ("AppSettings");
                }
            }

            root[SectionName] = new JsonObject
            {
                [nameof (LineNumbers)] = LineNumbers,
                [nameof (FoldIndicators)] = FoldIndicators,
                [nameof (WordWrap)] = WordWrap,
                [nameof (ShowTabs)] = ShowTabs,
                [nameof (IndentSize)] = IndentSize,
                [nameof (ConvertTabsToSpaces)] = ConvertTabsToSpaces,
                [nameof (AutoIndent)] = AutoIndent,
                [nameof (AutoComplete)] = AutoComplete,
                [nameof (Scrollbars)] = Scrollbars
            };

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

        // Tolerate the JSON TG itself accepts (// comments, trailing commas).
        JsonNode? node = JsonNode.Parse (
            text,
            documentOptions: new JsonDocumentOptions
            {
                CommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true
            });

        return node as JsonObject ?? new JsonObject ();
    }

    private static void ApplyLegacyDottedKeys (IConfiguration configuration, EditorSettingsValues settings)
    {
        ApplyLegacyBoolean (configuration, nameof (LineNumbers), value => settings.LineNumbers = value);
        ApplyLegacyBoolean (configuration, nameof (FoldIndicators), value => settings.FoldIndicators = value);
        ApplyLegacyBoolean (configuration, nameof (WordWrap), value => settings.WordWrap = value);
        ApplyLegacyBoolean (configuration, nameof (ShowTabs), value => settings.ShowTabs = value);
        ApplyLegacyInt32 (configuration, nameof (IndentSize), value => settings.IndentSize = value);
        ApplyLegacyBoolean (configuration, nameof (ConvertTabsToSpaces), value => settings.ConvertTabsToSpaces = value);
        ApplyLegacyBoolean (configuration, nameof (AutoIndent), value => settings.AutoIndent = value);
        ApplyLegacyBoolean (configuration, nameof (Scrollbars), value => settings.Scrollbars = value);
        ApplyLegacyBoolean (configuration, nameof (AutoComplete), value => settings.AutoComplete = value);
    }

    private static void ApplyLegacyBoolean (IConfiguration configuration, string propertyName, Action<bool> apply)
    {
        var value = configuration[$"{SectionName}.{propertyName}"];

        if (value is null)
        {
            return;
        }

        apply (bool.Parse (value));
    }

    private static void ApplyLegacyInt32 (IConfiguration configuration, string propertyName, Action<int> apply)
    {
        var value = configuration[$"{SectionName}.{propertyName}"];

        if (value is null)
        {
            return;
        }

        apply (int.Parse (value, CultureInfo.InvariantCulture));
    }

    private static void RemoveLegacyDottedKeys (JsonObject json)
    {
        foreach (var legacyKey in json
                     .Where (kvp => kvp.Key.StartsWith ($"{SectionName}.", StringComparison.Ordinal))
                     .Select (kvp => kvp.Key)
                     .ToList ())
        {
            json.Remove (legacyKey);
        }
    }

    internal sealed class EditorSettingsValues
    {
        public bool LineNumbers { get; set; } = true;

        public bool FoldIndicators { get; set; } = true;

        public bool WordWrap { get; set; }

        public bool ShowTabs { get; set; }

        public int IndentSize { get; set; } = 4;

        public bool ConvertTabsToSpaces { get; set; } = true;

        public bool AutoIndent { get; set; } = true;

        public bool Scrollbars { get; set; } = true;

        public bool AutoComplete { get; set; }
    }
}

#pragma warning restore CS0618
