// CoPilot - claude-sonnet-4-5

using Microsoft.Extensions.Configuration;
using Terminal.Gui.Editor.Configuration;
using Terminal.Gui.Editor.IntegrationTests.Testing;
using Terminal.Gui.Input;
using Terminal.Gui.Testing;
using Xunit;
using MecEditorSettings = Terminal.Gui.Editor.Configuration.EditorSettings;

namespace Terminal.Gui.Editor.IntegrationTests;

/// <summary>
///     Serialisation guard: <see cref="EditorKeyBindingIntegrationTests" /> mutates
///     <see cref="Editor.DefaultKeyBindings" />, a process-wide static that Terminal.Gui reads during view construction. Using
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
///                     Loading a Microsoft.Extensions.Configuration profile using the
///                     <c>"Editor:DefaultKeyBindings"</c> section.
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
    ///     Proves that loading a Microsoft.Extensions.Configuration profile with the
    ///     <c>"Editor:DefaultKeyBindings"</c> section causes the configured custom key to trigger
    ///     the correct command in a live <see cref="Editor" />.
    ///     <para>
    ///         The test JSON mirrors the structure from the issue:
    ///         <code>
    ///             {
    ///               "Editor": {
    ///                 "DefaultKeyBindings": {
    ///                   "Undo": { "All": ["Ctrl+U"] }
    ///                 }
    ///               }
    ///             }
    ///         </code>
    ///     </para>
    /// </summary>
    [Fact]
    public async Task MecConfiguration_CustomUndoKey_ActuallyUndoes ()
    {
        MecEditorSettings original = MecEditorSettings.Defaults;

        try
        {
            IConfiguration configuration = new ConfigurationBuilder ()
                                           .AddInMemoryCollection (new Dictionary<string, string?>
                                           {
                                               ["Editor:DefaultKeyBindings:Undo:All:0"] = "Ctrl+U"
                                           })
                                           .Build ();

            EditorConfiguration.Apply (configuration);

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
            MecEditorSettings.Defaults = original;
        }
    }
}
