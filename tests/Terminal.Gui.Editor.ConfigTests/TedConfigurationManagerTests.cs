// Claude - claude-opus-4-7

#pragma warning disable CS0618 // This project intentionally quarantines legacy ConfigurationManager coverage.
using Ted;
using Terminal.Gui.Configuration;
using Xunit;
using static Terminal.Gui.Configuration.ConfigurationManager;

namespace Terminal.Gui.Editor.ConfigTests;

/// <summary>
///     End-to-end proof that Terminal.Gui's <see cref="ConfigurationManager" /> is the read
///     authority for ted's settings: a <c>ted.config.json</c> body (app-defined
///     <see cref="AppSettingsScope" /> properties, nested under <c>"AppSettings"</c>, keyed
///     <c>EditorSettings.&lt;Name&gt;</c>) is loaded and applied to the <see cref="Ted.TedApp" />'s
///     <see cref="Terminal.Gui.Views.Editor" />.
///     <para>
///         This project exists solely for ConfigurationManager tests. CM is process-global with
///         one-time <see cref="ConfigurationPropertyAttribute" /> discovery, so it cannot share a
///         process with parallel tests — <c>xunit.runner.json</c> disables assembly and collection
///         parallelization here (the pattern Terminal.Gui itself uses for its CM suite). See
///         CLAUDE.md "Testing tiers".
///     </para>
/// </summary>
public class TedConfigurationManagerTests
{
    [Fact]
    public void ConfigurationManager_Applies_AppSettings_To_TedApp ()
    {
        try
        {
            // Clean, controlled baseline. (Defensive: this assembly is non-parallel and CM-only,
            // so nothing should have enabled CM, but never assume process-global state.)
            if (IsEnabled)
            {
                Disable (true);
            }

            ThrowOnJsonErrors = true;
            Enable (ConfigLocations.HardCoded);

            // ted.config.json shape CM requires for AppSettingsScope: nested under "AppSettings",
            // keyed DeclaringType.PropertyName. ThrowOnJsonErrors makes a wrong scope/key fail loudly.
            RuntimeConfig =
                """
                {
                  "AppSettings": {
                    "EditorSettings.WordWrap": true,
                    "EditorSettings.ShowTabs": true,
                    "EditorSettings.LineNumbers": false,
                    "EditorSettings.IndentSize": 2
                  }
                }
                """;
            Load (ConfigLocations.Runtime);
            Apply ();

            TedApp app = new ();

            // Assert via the Editor instance (TedApp seeds it from the EditorSettings statics CM set).
            Assert.True (app.Editor.WordWrap);
            Assert.True (app.Editor.ShowTabs);
            Assert.False (app.Editor.GutterOptions.HasFlag (GutterOptions.LineNumbers));
            Assert.Equal (2, app.Editor.IndentationSize);
        }
        finally
        {
            Disable (true);

            // Restore declared defaults so a later CM test in this assembly starts clean.
            EditorSettings.ResetDefaults ();
        }
    }

#pragma warning restore CS0618
}
