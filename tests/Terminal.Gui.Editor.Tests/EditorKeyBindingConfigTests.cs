// CoPilot - claude-sonnet-4-5

using Terminal.Gui.Configuration;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Xunit;

namespace Terminal.Gui.Editor.Tests;

/// <summary>
///     Serialisation guard: <see cref="EditorKeyBindingConfigTests" /> mutates
///     <see cref="Editor.DefaultKeyBindings" /> and <see cref="View.ViewKeyBindings" />, both
///     process-wide statics that Terminal.Gui reads during view construction. Running these tests
///     concurrently with other tests that create <see cref="Editor" /> instances would corrupt those
///     instances' <see cref="View.KeyBindings" />.
///     <c>DisableParallelization = true</c> serialises this collection against every other collection
///     in this assembly.
/// </summary>
[CollectionDefinition (nameof (KeyBindingConfigCollection), DisableParallelization = true)]
public sealed class KeyBindingConfigCollection;

/// <summary>
///     Unit tests that prove the keybinding configuration mechanism works.
///     <para>
///         Two approaches are covered:
///         <list type="number">
///             <item>
///                 <description>
///                     Direct mutation of <see cref="Editor.DefaultKeyBindings" /> — simulates what a
///                     custom bootstrap layer would do before the first <see cref="Editor" /> is
///                     constructed.
///                 </description>
///             </item>
///             <item>
///                 <description>
///                     <see cref="ConfigurationManager.RuntimeConfig" /> with the
///                     <c>"View.ViewKeyBindings"</c> JSON key — the standard Terminal.Gui per-view
///                     override mechanism that works for any view type including external-assembly
///                     views such as <see cref="Editor" />.
///                 </description>
///             </item>
///         </list>
///     </para>
///     <para>
///         Note on binding layering: Terminal.Gui's <see cref="View.ApplyKeyBindings" /> is additive.
///         When <see cref="Editor.DefaultKeyBindings" /> is replaced with a dictionary containing only
///         a few commands, the keys for those commands are <em>added</em> to whatever keys were already
///         bound by earlier layers (e.g. <see cref="View.DefaultKeyBindings" />). Tests therefore assert
///         that configured keys <em>are present</em> rather than that old keys are absent.
///     </para>
/// </summary>
[Collection (nameof (KeyBindingConfigCollection))]
public class EditorKeyBindingConfigTests
{
    /// <summary>
    ///     Verifies the editor's out-of-the-box key bindings match the documented defaults so that a
    ///     regression in <see cref="Editor.DefaultKeyBindings" /> is caught immediately.
    /// </summary>
    [Fact]
    public void DefaultKeyBindings_ContainsExpectedDefaults ()
    {
        Assert.NotNull (Editor.DefaultKeyBindings);
        Dictionary<Command, PlatformKeyBinding> defaults = Editor.DefaultKeyBindings!;

        // Undo/Redo
        Assert.True (defaults.ContainsKey (Command.Undo));
        Assert.True (defaults.ContainsKey (Command.Redo));

        // Clipboard
        Assert.True (defaults.ContainsKey (Command.Cut));
        Assert.True (defaults.ContainsKey (Command.Copy));
        Assert.True (defaults.ContainsKey (Command.Paste));

        // Editing
        Assert.True (defaults.ContainsKey (Command.NewLine));
        Assert.True (defaults.ContainsKey (Command.DeleteCharLeft));
        Assert.True (defaults.ContainsKey (Command.DeleteCharRight));

        // Document navigation
        Assert.True (defaults.ContainsKey (Command.Start));
        Assert.True (defaults.ContainsKey (Command.End));

        // Indentation
        Assert.True (defaults.ContainsKey (Command.InsertTab));
        Assert.True (defaults.ContainsKey (Command.Unindent));

        // Find / Replace
        Assert.True (defaults.ContainsKey (Command.Find));
        Assert.True (defaults.ContainsKey (Command.Replace));
        Assert.True (defaults.ContainsKey (Command.FindNext));
        Assert.True (defaults.ContainsKey (Command.FindPrevious));
    }

    /// <summary>
    ///     Verifies that <see cref="Editor.DefaultKeyBindings" /> ships the correct default keys for
    ///     the clipboard commands (Ctrl+X / Ctrl+C / Ctrl+V) by checking the resolved platform keys.
    /// </summary>
    [Fact]
    public void DefaultKeyBindings_ClipboardKeys_AreCorrect ()
    {
        Assert.NotNull (Editor.DefaultKeyBindings);
        Dictionary<Command, PlatformKeyBinding> defaults = Editor.DefaultKeyBindings!;

        Assert.Contains (Key.X.WithCtrl, defaults[Command.Cut].GetCurrentPlatformKeys ());
        Assert.Contains (Key.C.WithCtrl, defaults[Command.Copy].GetCurrentPlatformKeys ());
        Assert.Contains (Key.V.WithCtrl, defaults[Command.Paste].GetCurrentPlatformKeys ());
    }

    /// <summary>
    ///     Verifies that <see cref="Editor.DefaultKeyBindings" /> ships the correct default keys for
    ///     undo (Ctrl+Z) and redo (Ctrl+Y and Ctrl+Shift+Z).
    /// </summary>
    [Fact]
    public void DefaultKeyBindings_UndoRedoKeys_AreCorrect ()
    {
        Assert.NotNull (Editor.DefaultKeyBindings);
        Dictionary<Command, PlatformKeyBinding> defaults = Editor.DefaultKeyBindings!;

        Assert.Contains (Key.Z.WithCtrl, defaults[Command.Undo].GetCurrentPlatformKeys ());
        Assert.Contains (Key.Y.WithCtrl, defaults[Command.Redo].GetCurrentPlatformKeys ());
        Assert.Contains (Key.Z.WithCtrl.WithShift, defaults[Command.Redo].GetCurrentPlatformKeys ());
    }

    /// <summary>
    ///     Proves that replacing <see cref="Editor.DefaultKeyBindings" /> before constructing an
    ///     <see cref="Editor" /> causes the new instance to include the overridden binding.
    ///     This is the core mechanism that a bootstrap layer (or future CM integration) relies on when
    ///     it applies a JSON profile such as:
    ///     <code>
    ///         { "Terminal.Gui.Editor.Editor.DefaultKeyBindings": { "Cut": { "All": ["Ctrl+W"] } } }
    ///     </code>
    /// </summary>
    [Fact]
    public void Override_DefaultKeyBindings_NewEditor_PicksUp_CustomCutKey ()
    {
        Dictionary<Command, PlatformKeyBinding>? original = Editor.DefaultKeyBindings;

        try
        {
            Editor.DefaultKeyBindings = new Dictionary<Command, PlatformKeyBinding>
            {
                [Command.Cut] = Bind.All (Key.W.WithCtrl)
            };

            Editor editor = new ();

            // The custom Ctrl+W binding must be present.
            Assert.Contains (Key.W.WithCtrl, editor.KeyBindings.GetAllFromCommands (Command.Cut));
        }
        finally
        {
            Editor.DefaultKeyBindings = original;
        }
    }

    /// <summary>
    ///     Proves that setting <see cref="Editor.DefaultKeyBindings" /> to include a custom Copy key
    ///     (Ctrl+Shift+C as shown in the issue's example JSON) causes a new <see cref="Editor" /> to
    ///     include that key alongside any defaults.
    /// </summary>
    [Fact]
    public void Override_DefaultKeyBindings_NewEditor_PicksUp_CustomCopyKey ()
    {
        Dictionary<Command, PlatformKeyBinding>? original = Editor.DefaultKeyBindings;

        try
        {
            Editor.DefaultKeyBindings = new Dictionary<Command, PlatformKeyBinding>
            {
                [Command.Copy] = Bind.All (Key.C.WithCtrl.WithShift)
            };

            Editor editor = new ();

            Assert.Contains (Key.C.WithCtrl.WithShift, editor.KeyBindings.GetAllFromCommands (Command.Copy));
        }
        finally
        {
            Editor.DefaultKeyBindings = original;
        }
    }

    /// <summary>
    ///     Proves that a new <see cref="Editor" /> created after the default bindings are overridden does
    ///     not affect an existing instance whose <see cref="View.KeyBindings" /> were already applied.
    /// </summary>
    [Fact]
    public void Override_DefaultKeyBindings_ExistingEditor_IsNotAffected ()
    {
        Dictionary<Command, PlatformKeyBinding>? original = Editor.DefaultKeyBindings;

        try
        {
            // Create an editor with the original defaults.
            Editor editorBefore = new ();
            IEnumerable<Key> cutKeysBefore = editorBefore.KeyBindings.GetAllFromCommands (Command.Cut).ToList ();

            // Now override the defaults.
            Editor.DefaultKeyBindings = new Dictionary<Command, PlatformKeyBinding>
            {
                [Command.Cut] = Bind.All (Key.W.WithCtrl)
            };

            // A second editor picks up the override.
            Editor editorAfter = new ();
            Assert.Contains (Key.W.WithCtrl, editorAfter.KeyBindings.GetAllFromCommands (Command.Cut));

            // The already-constructed editor must still have whatever keys it had before the override.
            Assert.Equal (cutKeysBefore, editorBefore.KeyBindings.GetAllFromCommands (Command.Cut));
        }
        finally
        {
            Editor.DefaultKeyBindings = original;
        }
    }

    /// <summary>
    ///     Proves that loading a JSON keybinding profile via
    ///     <see cref="ConfigurationManager.RuntimeConfig" /> using the standard Terminal.Gui
    ///     <c>"View.ViewKeyBindings"</c> key updates the <see cref="Editor" /> instance's key
    ///     bindings. This is the CM-native path for per-view binding configuration.
    /// </summary>
    /// <remarks>
    ///     The JSON key format is:
    ///     <code>
    ///         {
    ///           "View.ViewKeyBindings": {
    ///             "Editor": {
    ///               "Cut":  { "All": ["Ctrl+W"] },
    ///               "Copy": { "All": ["Ctrl+Shift+C"] },
    ///               "Undo": { "All": ["Ctrl+Z"] }
    ///             }
    ///           }
    ///         }
    ///     </code>
    ///     This mirrors the structure of the issue's example JSON and proves the end-to-end
    ///     config→Editor pipeline works.
    /// </remarks>
    [Fact]
    public void ConfigurationManager_RuntimeConfig_ViewKeyBindings_UpdatesEditorBindings ()
    {
        Dictionary<string, Dictionary<Command, PlatformKeyBinding>>? originalViewKeyBindings = View.ViewKeyBindings;
        var originalRuntimeConfig = ConfigurationManager.RuntimeConfig;
        var wasEnabled = ConfigurationManager.IsEnabled;

        try
        {
            const string json = """
                                {
                                  "View.ViewKeyBindings": {
                                    "Editor": {
                                      "Cut":  { "All": ["Ctrl+W"] },
                                      "Copy": { "All": ["Ctrl+Shift+C"] },
                                      "Undo": { "All": ["Ctrl+Z"] }
                                    }
                                  }
                                }
                                """;

            ConfigurationManager.RuntimeConfig = json;
            ConfigurationManager.Enable (ConfigLocations.Runtime);

            // View.ViewKeyBindings must have been populated for the "Editor" type.
            Assert.NotNull (View.ViewKeyBindings);
            Assert.True (View.ViewKeyBindings!.ContainsKey ("Editor"),
                "ViewKeyBindings must contain an entry for 'Editor'");

            // A new Editor must include the configured bindings.
            Editor editor = new ();

            Assert.Contains (Key.W.WithCtrl, editor.KeyBindings.GetAllFromCommands (Command.Cut));
            Assert.Contains (Key.C.WithCtrl.WithShift, editor.KeyBindings.GetAllFromCommands (Command.Copy));
            Assert.Contains (Key.Z.WithCtrl, editor.KeyBindings.GetAllFromCommands (Command.Undo));
        }
        finally
        {
            // Always restore global state, even when the test fails.
            View.ViewKeyBindings = originalViewKeyBindings;
            ConfigurationManager.RuntimeConfig = originalRuntimeConfig;

            if (!wasEnabled)
            {
                ConfigurationManager.Disable (true);
            }
        }
    }
}
