// Claude - gpt-5

using System.Reflection;
using Ted;
using Xunit;

namespace Terminal.Gui.Editor.IntegrationTests;

[CollectionDefinition (nameof (TedSettingsPersistenceCollection), DisableParallelization = true)]
public sealed class TedSettingsPersistenceCollection;

[Collection (nameof (TedSettingsPersistenceCollection))]
public class TedSettingsPersistenceTests
{
    [Fact]
    public void SaveViewSettings_Creates_TedConfigFile_In_HomeDotTui ()
    {
        string home = CreateTempHome ();
        string? originalHome = Environment.GetEnvironmentVariable ("HOME");

        try
        {
            Environment.SetEnvironmentVariable ("HOME", home);
            TedApp app = new ();
            app.Editor.IndentationSize = 7;

            InvokeSaveViewSettings (app);

            var configPath = Path.Combine (home, ".tui", "ted.config.json");
            Assert.True (File.Exists (configPath));
            Assert.Contains ("\"EditorSettings.IndentSize\": 7", File.ReadAllText (configPath));
        }
        finally
        {
            Environment.SetEnvironmentVariable ("HOME", originalHome);
            DeleteTempHome (home);
        }
    }

    [Fact]
    public void SaveViewSettings_Updates_Existing_IndentSize_Value ()
    {
        string home = CreateTempHome ();
        string? originalHome = Environment.GetEnvironmentVariable ("HOME");

        try
        {
            Environment.SetEnvironmentVariable ("HOME", home);
            TedApp app = new ();

            app.Editor.IndentationSize = 2;
            InvokeSaveViewSettings (app);

            app.Editor.IndentationSize = 8;
            InvokeSaveViewSettings (app);

            var configPath = Path.Combine (home, ".tui", "ted.config.json");
            string text = File.ReadAllText (configPath);
            Assert.Contains ("\"EditorSettings.IndentSize\": 8", text);
            Assert.DoesNotContain ("\"EditorSettings.IndentSize\": 2", text);
        }
        finally
        {
            Environment.SetEnvironmentVariable ("HOME", originalHome);
            DeleteTempHome (home);
        }
    }

    private static string CreateTempHome ()
    {
        string home = Path.Combine (Path.GetTempPath (), $"ted-home-{Guid.NewGuid ():N}");
        Directory.CreateDirectory (home);

        return home;
    }

    private static void DeleteTempHome (string home)
    {
        if (Directory.Exists (home))
        {
            Directory.Delete (home, true);
        }
    }

    private static void InvokeSaveViewSettings (TedApp app)
    {
        MethodInfo? saveViewSettings = typeof (TedApp).GetMethod (
            "SaveViewSettings",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull (saveViewSettings);
        saveViewSettings.Invoke (app, null);
    }
}
