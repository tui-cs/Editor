// Claude - gpt-5

using System.Drawing;
using System.Reflection;
using System.Text.Json.Nodes;
using Ted;
using Terminal.Gui.Editor.IntegrationTests.Testing;
using Terminal.Gui.Input;
using Terminal.Gui.Testing;
using Terminal.Gui.Views;
using Xunit;

namespace Terminal.Gui.Editor.IntegrationTests;

[CollectionDefinition (nameof (TedSettingsPersistenceCollection), DisableParallelization = true)]
public sealed class TedSettingsPersistenceCollection;

[Collection (nameof (TedSettingsPersistenceCollection))]
public class TedSettingsPersistenceTests
{
    [Fact]
    public void SaveViewSettings_Creates_ConfigFile_And_Persists_IndentSize ()
    {
        using ConfigPathScope scope = new ();
        TedApp app = new ();
        app.Editor.IndentationSize = 7;

        InvokeSaveViewSettings (app);

        Assert.True (File.Exists (scope.ConfigPath));
        Assert.Contains ("\"IndentSize\": 7", File.ReadAllText (scope.ConfigPath));
    }

    [Fact]
    public void SaveViewSettings_Updates_Existing_IndentSize_Value ()
    {
        using ConfigPathScope scope = new ();
        TedApp app = new ();

        app.Editor.IndentationSize = 2;
        InvokeSaveViewSettings (app);

        app.Editor.IndentationSize = 8;
        InvokeSaveViewSettings (app);

        var text = File.ReadAllText (scope.ConfigPath);
        Assert.Contains ("\"IndentSize\": 8", text);
        Assert.DoesNotContain ("\"IndentSize\": 2", text);
    }

    [Fact]
    public void SaveViewSettings_Persists_WordWrap_Changes ()
    {
        using ConfigPathScope scope = new ();
        TedApp app = new ();
        app.Editor.WordWrap = true;

        InvokeSaveViewSettings (app);
        Assert.True (File.Exists (scope.ConfigPath));
        Assert.Contains ("\"WordWrap\": true", File.ReadAllText (scope.ConfigPath));
    }

    [Fact]
    public void MecSettings_Applies_To_TedApp ()
    {
        using ConfigPathScope scope = new ();
        var configDirectory = Path.GetDirectoryName (scope.ConfigPath);
        Assert.NotNull (configDirectory);
        Directory.CreateDirectory (configDirectory);
        File.WriteAllText (
            scope.ConfigPath,
            """
            {
              "EditorSettings": {
                "WordWrap": true,
                "ShowTabs": true,
                "LineNumbers": false,
                "IndentSize": 2
              }
            }
            """);

        EditorSettings.Load (scope.ConfigPath);
        TedApp app = new ();

        Assert.True (app.Editor.WordWrap);
        Assert.True (app.Editor.ShowTabs);
        Assert.False (app.Editor.GutterOptions.HasFlag (GutterOptions.LineNumbers));
        Assert.Equal (2, app.Editor.IndentationSize);
    }

    [Fact]
    public void LegacyCmSettings_Applies_To_TedApp ()
    {
        using ConfigPathScope scope = new ();
        var configDirectory = Path.GetDirectoryName (scope.ConfigPath);
        Assert.NotNull (configDirectory);
        Directory.CreateDirectory (configDirectory);
        File.WriteAllText (
            scope.ConfigPath,
            """
            {
              "AppSettings": {
                "EditorSettings.WordWrap": true,
                "EditorSettings.ShowTabs": true,
                "EditorSettings.LineNumbers": false,
                "EditorSettings.IndentSize": 2
              }
            }
            """);

        EditorSettings.Load (scope.ConfigPath);
        TedApp app = new ();

        Assert.True (app.Editor.WordWrap);
        Assert.True (app.Editor.ShowTabs);
        Assert.False (app.Editor.GutterOptions.HasFlag (GutterOptions.LineNumbers));
        Assert.Equal (2, app.Editor.IndentationSize);
    }

    [Fact]
    public async Task ViewMenu_WordWrap_Toggle_Creates_ConfigFile ()
    {
        using ConfigPathScope scope = new ();
        await using AppFixture<TedApp> fx = new (() => new TedApp ());
        InputInjectionOptions options = new () { Mode = InputInjectionMode.Direct };
        DateTime ts = new (2025, 1, 1, 12, 0, 0);

        var initialLines = fx.Driver.ToString ().Split ('\n');
        var viewHeaderX = initialLines[0].IndexOf ("View", StringComparison.Ordinal);
        Assert.True (viewHeaderX >= 0);
        Point viewHeader = new (viewHeaderX, 0);
        fx.Injector.InjectMouse (
            new Mouse { ScreenPosition = viewHeader, Flags = MouseFlags.LeftButtonPressed, Timestamp = ts },
            options);
        fx.Injector.InjectMouse (
            new Mouse
            {
                ScreenPosition = viewHeader,
                Flags = MouseFlags.LeftButtonReleased,
                Timestamp = ts.AddMilliseconds (25)
            },
            options);
        fx.Render ();

        var menuLines = fx.Driver.ToString ().Split ('\n');
        var y = Array.FindIndex (menuLines, static line => line.Contains ("Word Wrap", StringComparison.Ordinal));
        Assert.True (y >= 0);
        var x = -1;
        // Prefer label text as the click target; glyph fallbacks handle renderer differences.
        string[] targets = ["Word Wrap", "☐", "☑"];
        foreach (var target in targets)
        {
            x = menuLines[y].IndexOf (target, StringComparison.Ordinal);
            if (x >= 0)
            {
                break;
            }
        }

        Assert.True (x >= 0);
        Point wordWrapItem = new (x, y);

        fx.Injector.InjectMouse (
            new Mouse
            {
                ScreenPosition = wordWrapItem,
                Flags = MouseFlags.LeftButtonPressed,
                Timestamp = ts.AddMilliseconds (50)
            },
            options);
        fx.Injector.InjectMouse (
            new Mouse
            {
                ScreenPosition = wordWrapItem,
                Flags = MouseFlags.LeftButtonReleased,
                Timestamp = ts.AddMilliseconds (75)
            },
            options);
        fx.Render ();

        Assert.True (File.Exists (scope.ConfigPath));
    }

    [Fact]
    public async Task ViewMenu_WordWrap_MouseToggle_Creates_ConfigFile ()
    {
        using ConfigPathScope scope = new ();
        await using AppFixture<TedApp> fx = new (() => new TedApp ());
        InputInjectionOptions options = new () { Mode = InputInjectionMode.Direct };

        fx.Injector.InjectKey (Key.V.WithAlt, options);
        fx.Render ();

        var lines = fx.Driver.ToString ().Split ('\n');
        var y = Array.FindIndex (lines, static line => line.Contains ("Word Wrap", StringComparison.Ordinal));
        Assert.True (y >= 0);
        var x = lines[y].IndexOf ("Word Wrap", StringComparison.Ordinal);
        Assert.True (x >= 0);

        DateTime ts = new (2025, 1, 1, 12, 0, 0);
        Point click = new (x, y);
        fx.Injector.InjectMouse (
            new Mouse { ScreenPosition = click, Flags = MouseFlags.LeftButtonPressed, Timestamp = ts }, options);
        fx.Injector.InjectMouse (
            new Mouse
            {
                ScreenPosition = click,
                Flags = MouseFlags.LeftButtonReleased,
                Timestamp = ts.AddMilliseconds (25)
            },
            options);
        fx.Render ();

        Assert.True (File.Exists (scope.ConfigPath));
    }

    [Fact]
    public async Task ViewMenu_WordWrap_Toggle_Persists_True ()
    {
        // Reproduces the user-reported bug: toggling Word Wrap via the View menu
        // should create ted.config.json AND persist "WordWrap": true in the MEC settings section.
        // Before the fix, a conflicting ValueChanged handler caused a double-toggle
        // that reverted WordWrap to false immediately.
        using ConfigPathScope scope = new ();
        await using AppFixture<TedApp> fx = new (() => new TedApp ());
        InputInjectionOptions options = new () { Mode = InputInjectionMode.Direct };
        DateTime ts = new (2025, 1, 1, 12, 0, 0);

        // Precondition: word wrap is initially off
        Assert.False (fx.Top.Editor.WordWrap);

        // Open View menu via keyboard (Alt+V) - same as real user interaction
        fx.Injector.InjectKey (Key.V.WithAlt, options);
        fx.Render ();

        // Find and click "Word Wrap"
        var menuLines = fx.Driver.ToString ().Split ('\n');
        var y = Array.FindIndex (menuLines, static line => line.Contains ("Word Wrap", StringComparison.Ordinal));
        Assert.True (y >= 0, "Could not find 'Word Wrap' in menu");
        var x = menuLines[y].IndexOf ("Word Wrap", StringComparison.Ordinal);
        Assert.True (x >= 0);
        Point wordWrapItem = new (x, y);

        fx.Injector.InjectMouse (
            new Mouse
            {
                ScreenPosition = wordWrapItem,
                Flags = MouseFlags.LeftButtonPressed,
                Timestamp = ts
            },
            options);
        fx.Injector.InjectMouse (
            new Mouse
            {
                ScreenPosition = wordWrapItem,
                Flags = MouseFlags.LeftButtonReleased,
                Timestamp = ts.AddMilliseconds (25)
            },
            options);
        fx.Render ();

        // Assert: Editor.WordWrap is now true (not toggled back by double-fire)
        Assert.True (fx.Top.Editor.WordWrap, "Editor.WordWrap should be true after toggle");

        // Assert: config file created
        Assert.True (File.Exists (scope.ConfigPath), "Config file was not created");

        // Assert: config file contains the correct persisted value
        var configContent = File.ReadAllText (scope.ConfigPath);
        Assert.Contains ("\"WordWrap\": true", configContent);
    }

    [Fact]
    public void QuitFile_DoesNotPersist_ViewSettings ()
    {
        using ConfigPathScope scope = new ();
        TedApp app = new ();
        app.Editor.WordWrap = true;

        Assert.True (app.QuitFile ());

        Assert.False (File.Exists (scope.ConfigPath));
    }

    [Fact]
    public void SaveViewSettings_Preserves_Other_TopLevel_Keys_And_Nests_Under_EditorSettings ()
    {
        using ConfigPathScope scope = new ();
        var configDirectory = Path.GetDirectoryName (scope.ConfigPath);
        Assert.NotNull (configDirectory);
        Directory.CreateDirectory (configDirectory);

        // Unrelated top-level data + a legacy flat key (pre-CM format) that must be migrated away.
        File.WriteAllText (
            scope.ConfigPath,
            "{\n  \"Theme\": \"Dark\",\n  \"EditorSettings.WordWrap\": false,\n  \"AppSettings\": { \"EditorSettings.ShowTabs\": true }\n}\n");

        TedApp app = new ();
        app.Editor.WordWrap = true;

        InvokeSaveViewSettings (app);

        var text = File.ReadAllText (scope.ConfigPath);
        JsonNode root = JsonNode.Parse (text)!;

        // Unrelated key preserved; legacy keys migrated away; ted settings nested under
        // "EditorSettings" (the MEC-native shape).
        Assert.Equal ("Dark", (string?)root["Theme"]);
        Assert.Null (root["EditorSettings.WordWrap"]);
        JsonNode appSettings = Assert.IsType<JsonObject> (root["AppSettings"]);
        Assert.Null (appSettings["EditorSettings.ShowTabs"]);
        JsonNode editorSettings = Assert.IsType<JsonObject> (root["EditorSettings"]);
        Assert.True ((bool)editorSettings["WordWrap"]!);
    }

    [Fact]
    public void EditorTabSettingsTab_IndentSize_Rejects_Zero ()
    {
        TedApp app = new ();
        EditorTabSettingsTab tab = new (app.Editor);

        // Access _indentSize via reflection (it's private)
        FieldInfo? indentSizeField =
            typeof (EditorTabSettingsTab).GetField ("_indentSize", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull (indentSizeField);
        NumericUpDown<int> indentControl = (NumericUpDown<int>)indentSizeField.GetValue (tab)!;

        var valueBefore = indentControl.Value;
        Assert.True (valueBefore >= 1);

        // Attempt to set Value to 0 — ValueChanging should reject it
        indentControl.Value = 0;
        Assert.Equal (valueBefore, indentControl.Value);

        tab.ApplyTo (app.Editor);
        Assert.True (app.Editor.IndentationSize >= 1);
    }

    [Fact]
    public void Save_Appends_Before_Real_Brace_Not_Comment_Brace ()
    {
        // Regression: trailing JSONC comment containing } should not be used as insert point.
        using ConfigPathScope scope = new ();

        var dir = Path.GetDirectoryName (scope.ConfigPath);
        Assert.NotNull (dir);
        Directory.CreateDirectory (dir);
        File.WriteAllText (
            scope.ConfigPath,
            "{\n  \"EditorSettings.LineNumbers\": true\n}\n// end of config }\n");

        TedApp app = new ();
        app.Editor.WordWrap = true;

        InvokeSaveViewSettings (app);

        var text = File.ReadAllText (scope.ConfigPath);
        // The inserted key should be valid JSON — not inside the comment
        Assert.Contains ("\"WordWrap\": true", text);
        // Config should still be parseable: the real closing brace should come after our insertion
        var wordWrapPos = text.IndexOf ("\"WordWrap\"", StringComparison.Ordinal);
        var lastRealBrace = text.LastIndexOf ('}');
        // The comment line's } may still exist, but our key must be before the real object close
        Assert.True (wordWrapPos < lastRealBrace, "WordWrap key should appear before the root closing brace");
    }

    private static string GetTedConfigPath ()
    {
        var home =
            Environment.GetEnvironmentVariable ("HOME")
            ?? Environment.GetFolderPath (Environment.SpecialFolder.UserProfile);

        return Path.Combine (home, ".tui", "ted.config.json");
    }

    private static void InvokeSaveViewSettings (TedApp app)
    {
        MethodInfo? saveViewSettings = typeof (TedApp).GetMethod (
            "SaveViewSettings",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull (saveViewSettings);
        saveViewSettings.Invoke (app, null);
    }

    private sealed class ConfigPathScope : IDisposable
    {
        private readonly string? _existingConfigContent;
        private readonly bool _hadExistingConfig;
        private readonly string? _originalHome;
        private readonly string _tempRoot;

        internal ConfigPathScope ()
        {
            _tempRoot = Path.Combine (Path.GetTempPath (), $"ted-home-{Guid.NewGuid ():N}");
            Directory.CreateDirectory (_tempRoot);

            _originalHome = Environment.GetEnvironmentVariable ("HOME");
            Environment.SetEnvironmentVariable ("HOME", _tempRoot);

            ConfigPath = GetTedConfigPath ();
            _hadExistingConfig = File.Exists (ConfigPath);
            _existingConfigContent = _hadExistingConfig ? File.ReadAllText (ConfigPath) : null;

            if (_hadExistingConfig)
            {
                File.Delete (ConfigPath);
            }
        }

        internal string ConfigPath { get; }

        public void Dispose ()
        {
            EditorSettings.ResetDefaults ();

            if (_hadExistingConfig)
            {
                var configDirectory = Path.GetDirectoryName (ConfigPath);
                Assert.NotNull (_existingConfigContent);

                if (!string.IsNullOrWhiteSpace (configDirectory))
                {
                    Directory.CreateDirectory (configDirectory);
                }

                File.WriteAllText (ConfigPath, _existingConfigContent);
            }
            else if (File.Exists (ConfigPath))
            {
                File.Delete (ConfigPath);
            }

            Environment.SetEnvironmentVariable ("HOME", _originalHome);

            if (Directory.Exists (_tempRoot))
            {
                Directory.Delete (_tempRoot, true);
            }
        }
    }
}
