// Claude - gpt-5

using System.Drawing;
using System.Reflection;
using Ted;
using Terminal.Gui.Input;
using Terminal.Gui.Editor.IntegrationTests.Testing;
using Terminal.Gui.Testing;
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
        Assert.Contains ("\"EditorSettings.IndentSize\": 7", File.ReadAllText (scope.ConfigPath));
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

        string text = File.ReadAllText (scope.ConfigPath);
        Assert.Contains ("\"EditorSettings.IndentSize\": 8", text);
        Assert.DoesNotContain ("\"EditorSettings.IndentSize\": 2", text);
    }

    [Fact]
    public void SaveViewSettings_Persists_WordWrap_Changes ()
    {
        using ConfigPathScope scope = new ();
        TedApp app = new ();
        app.Editor.WordWrap = true;

        InvokeSaveViewSettings (app);
        Assert.True (File.Exists (scope.ConfigPath));
        Assert.Contains ("\"EditorSettings.WordWrap\": true", File.ReadAllText (scope.ConfigPath));
    }

    [Fact]
    public async Task ViewMenu_WordWrap_Toggle_Creates_ConfigFile ()
    {
        using ConfigPathScope scope = new ();
        await using AppFixture<TedApp> fx = new (() => new TedApp ());
        InputInjectionOptions options = new () { Mode = InputInjectionMode.Direct };
        DateTime ts = new (2025, 1, 1, 12, 0, 0);

        string[] initialLines = fx.Driver.ToString ().Split ('\n');
        int viewHeaderX = initialLines[0].IndexOf ("View", StringComparison.Ordinal);
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

        string[] menuLines = fx.Driver.ToString ().Split ('\n');
        int y = Array.FindIndex (menuLines, static line => line.Contains ("Word Wrap", StringComparison.Ordinal));
        Assert.True (y >= 0);
        int x = -1;
        string[] targets = ["☐", "☑", "Word Wrap"];
        foreach (string target in targets)
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

        string[] lines = fx.Driver.ToString ().Split ('\n');
        int y = Array.FindIndex (lines, static line => line.Contains ("Word Wrap", StringComparison.Ordinal));
        Assert.True (y >= 0);
        int x = lines[y].IndexOf ("☐", StringComparison.Ordinal);
        Assert.True (x >= 0);

        DateTime ts = new (2025, 1, 1, 12, 0, 0);
        Point click = new (x, y);
        fx.Injector.InjectMouse (new Mouse { ScreenPosition = click, Flags = MouseFlags.LeftButtonPressed, Timestamp = ts }, options);
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
    public void QuitFile_DoesNotPersist_ViewSettings ()
    {
        using ConfigPathScope scope = new ();
        TedApp app = new ();
        app.Editor.WordWrap = true;

        Assert.True (app.QuitFile ());

        Assert.False (File.Exists (scope.ConfigPath));
    }

    [Fact]
    public void SaveViewSettings_Inserts_Comma_Before_Trailing_Comment_When_Appending_Settings ()
    {
        using ConfigPathScope scope = new ();
        string? configDirectory = Path.GetDirectoryName (scope.ConfigPath);
        Assert.NotNull (configDirectory);
        Directory.CreateDirectory (configDirectory);
        File.WriteAllText (scope.ConfigPath, "{\n  \"Unrelated\": 1 // note\n}\n");

        TedApp app = new ();
        app.Editor.WordWrap = true;

        InvokeSaveViewSettings (app);

        string text = File.ReadAllText (scope.ConfigPath);
        Assert.Contains ("\"Unrelated\": 1, // note", text);
        Assert.Contains ("\"EditorSettings.WordWrap\": true", text);
    }

    [Fact]
    public void SettingsDialog_ApplyTo_Clamps_IndentSize_To_One ()
    {
        TedApp app = new ();
        // Reflection is used because EditorSettingsDialog is internal to the ted assembly.
        Type? dialogType = typeof (TedApp).Assembly.GetType ("Ted.EditorSettingsDialog");
        Assert.NotNull (dialogType);

        object? dialog = Activator.CreateInstance (
            dialogType,
            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public,
            binder: null,
            args: [app.Editor],
            culture: null);
        Assert.NotNull (dialog);

        FieldInfo? indentSizeField = dialogType.GetField ("_indentSize", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull (indentSizeField);
        object? indentControl = indentSizeField.GetValue (dialog);
        Assert.NotNull (indentControl);

        PropertyInfo? valueProperty = indentControl.GetType ().GetProperty ("Value");
        Assert.NotNull (valueProperty);
        valueProperty.SetValue (indentControl, 0);

        MethodInfo? applyTo = dialogType.GetMethod ("ApplyTo", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        Assert.NotNull (applyTo);
        applyTo.Invoke (dialog, [app.Editor]);

        Assert.Equal (1, app.Editor.IndentationSize);
    }

    private static string GetTedConfigPath ()
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

        return Path.Combine (baseDirectory, "ted.config.json");
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
        private readonly string _tempRoot;
        private readonly string? _originalHome;
        private readonly string? _originalAppData;
        private readonly bool _hadExistingConfig;
        private readonly string? _existingConfigContent;

        internal ConfigPathScope ()
        {
            _tempRoot = Path.Combine (Path.GetTempPath (), $"ted-home-{Guid.NewGuid ():N}");
            Directory.CreateDirectory (_tempRoot);

            _originalHome = Environment.GetEnvironmentVariable ("HOME");
            _originalAppData = Environment.GetEnvironmentVariable ("APPDATA");
            Environment.SetEnvironmentVariable ("HOME", _tempRoot);
            Environment.SetEnvironmentVariable ("APPDATA", Path.Combine (_tempRoot, "appdata"));

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
            if (_hadExistingConfig)
            {
                string? configDirectory = Path.GetDirectoryName (ConfigPath);
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
            Environment.SetEnvironmentVariable ("APPDATA", _originalAppData);

            if (Directory.Exists (_tempRoot))
            {
                Directory.Delete (_tempRoot, true);
            }
        }
    }
}
