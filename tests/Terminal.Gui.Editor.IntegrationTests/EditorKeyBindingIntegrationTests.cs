// CoPilot - claude-sonnet-4-5

using Terminal.Gui.Configuration;
using Terminal.Gui.Editor.IntegrationTests.Testing;
using Terminal.Gui.Input;
using Terminal.Gui.Testing;
using Terminal.Gui.ViewBase;
using Xunit;

namespace Terminal.Gui.Editor.IntegrationTests;

/// <summary>
///     Serialisation guard: <see cref="EditorKeyBindingIntegrationTests" /> mutates
///     <see cref="Editor.DefaultKeyBindings" /> and <see cref="View.ViewKeyBindings" />, both
///     process-wide statics that Terminal.Gui reads during view construction. Using
///     <c>DisableParallelization = true</c> serialises this collection against every other collection.
/// </summary>
[CollectionDefinition (nameof (KeyBindingIntegrationCollection), DisableParallelization = true)]
public sealed class KeyBindingIntegrationCollection;

/// <summary>
///     End-to-end tests that boot an <see cref="EditorTestHost" /> and inject synthetic keystrokes
///     to verify that custom keybinding configuration actually fires the expected commands.
///     <para>
///         These tests prove that both configuration paths work in a live <see cref="Editor" />:
///         <list type="bullet">
///             <item>
///                 <description>
///                     Direct mutation of <see cref="Editor.DefaultKeyBindings" />.
///                 </description>
///             </item>
///             <item>
///                 <description>
///                     Loading a JSON profile via <see cref="ConfigurationManager.RuntimeConfig" />
///                     using the <c>"View.ViewKeyBindings"</c> key.
///                 </description>
///             </item>
///         </list>
///     </para>
/// </summary>
[Collection (nameof (KeyBindingIntegrationCollection))]
public class EditorKeyBindingIntegrationTests
{
    private static readonly InputInjectionOptions Direct = new () { Mode = InputInjectionMode.Direct };

    /// <summary>
    ///     Proves that overriding <see cref="Editor.DefaultKeyBindings" /> to bind Undo to a custom key
    ///     causes that key to actually undo edits when injected into a live editor.
    /// </summary>
    [Fact]
    public async Task Override_DefaultKeyBindings_CustomUndoKey_ActuallyUndoes ()
    {
        Dictionary<Command, PlatformKeyBinding>? original = Editor.DefaultKeyBindings;

        try
        {
            // Rebind Undo to Ctrl+U instead of the default Ctrl+Z.
            Editor.DefaultKeyBindings = new Dictionary<Command, PlatformKeyBinding>
            {
                [Command.Undo] = Bind.All (Key.U.WithCtrl)
            };

            await using AppFixture<EditorTestHost> fx = new (() => new EditorTestHost ("abc"));
            fx.Top.Editor.SetFocus ();
            fx.Top.Editor.Document?.Insert (3, "DEF");

            Assert.Equal ("abcDEF", fx.Top.Editor.Document?.Text);

            // Inject the custom undo key.
            fx.Injector.InjectKey (Key.U.WithCtrl, Direct);

            Assert.Equal ("abc", fx.Top.Editor.Document?.Text);
        }
        finally
        {
            Editor.DefaultKeyBindings = original;
        }
    }

    /// <summary>
    ///     Proves that loading a JSON profile via <see cref="ConfigurationManager.RuntimeConfig" />
    ///     with the <c>"View.ViewKeyBindings"</c> key causes the configured custom key to trigger
    ///     the correct command in a live <see cref="Editor" />.
    ///     <para>
    ///         The test JSON mirrors the structure from the issue:
    ///         <code>
    ///             {
    ///               "View.ViewKeyBindings": {
    ///                 "Editor": {
    ///                   "Undo": { "All": ["Ctrl+U"] }
    ///                 }
    ///               }
    ///             }
    ///         </code>
    ///     </para>
    /// </summary>
    [Fact]
    public async Task ConfigurationManager_RuntimeConfig_CustomUndoKey_ActuallyUndoes ()
    {
        Dictionary<string, Dictionary<Command, PlatformKeyBinding>>? originalVKB = View.ViewKeyBindings;
        var originalRuntimeConfig = ConfigurationManager.RuntimeConfig;
        var wasEnabled = ConfigurationManager.IsEnabled;

        try
        {
            const string json = """
                                {
                                  "View.ViewKeyBindings": {
                                    "Editor": {
                                      "Undo": { "All": ["Ctrl+U"] }
                                    }
                                  }
                                }
                                """;

            ConfigurationManager.RuntimeConfig = json;
            ConfigurationManager.Enable (ConfigLocations.Runtime);

            await using AppFixture<EditorTestHost> fx = new (() => new EditorTestHost ("abc"));
            fx.Top.Editor.SetFocus ();
            fx.Top.Editor.Document?.Insert (3, "DEF");

            Assert.Equal ("abcDEF", fx.Top.Editor.Document?.Text);

            // Inject the key from the JSON config.
            fx.Injector.InjectKey (Key.U.WithCtrl, Direct);

            Assert.Equal ("abc", fx.Top.Editor.Document?.Text);
        }
        finally
        {
            View.ViewKeyBindings = originalVKB;
            ConfigurationManager.RuntimeConfig = originalRuntimeConfig;

            if (!wasEnabled)
            {
                ConfigurationManager.Disable (true);
            }
        }
    }
}
