// CoPilot - claude-sonnet-4-5

using Microsoft.Extensions.Configuration;
using Terminal.Gui.Editor.Configuration;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Xunit;
using MecEditorSettings = Terminal.Gui.Editor.Configuration.EditorSettings;

namespace Terminal.Gui.Editor.Tests;

/// <summary>
///     Serialisation guard: <see cref="EditorKeyBindingConfigTests" /> mutates
///     <see cref="Editor.DefaultKeyBindings" />, a process-wide static that Terminal.Gui reads during view construction. Running these tests
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
///                     Microsoft.Extensions.Configuration with the <c>"Editor"</c> section — the
///                     MEC path for external-assembly view defaults such as <see cref="Editor" />.
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

        // Word navigation
        Assert.True (defaults.ContainsKey (Command.WordLeft));
        Assert.True (defaults.ContainsKey (Command.WordRight));
        Assert.True (defaults.ContainsKey (Command.WordLeftExtend));
        Assert.True (defaults.ContainsKey (Command.WordRightExtend));
        Assert.True (defaults.ContainsKey (Command.KillWordLeft));
        Assert.True (defaults.ContainsKey (Command.KillWordRight));
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
    ///     word navigation (Ctrl+Left / Ctrl+Right and their Shift / Backspace / Delete variants).
    /// </summary>
    [Fact]
    public void DefaultKeyBindings_WordNavigationKeys_AreCorrect ()
    {
        Assert.NotNull (Editor.DefaultKeyBindings);
        Dictionary<Command, PlatformKeyBinding> defaults = Editor.DefaultKeyBindings!;

        Assert.Contains (Key.CursorLeft.WithCtrl, defaults[Command.WordLeft].GetCurrentPlatformKeys ());
        Assert.Contains (Key.CursorRight.WithCtrl, defaults[Command.WordRight].GetCurrentPlatformKeys ());
        Assert.Contains (Key.CursorLeft.WithCtrl.WithShift, defaults[Command.WordLeftExtend].GetCurrentPlatformKeys ());
        Assert.Contains (Key.CursorRight.WithCtrl.WithShift,
            defaults[Command.WordRightExtend].GetCurrentPlatformKeys ());
        Assert.Contains (Key.Backspace.WithCtrl, defaults[Command.KillWordLeft].GetCurrentPlatformKeys ());
        Assert.Contains (Key.Delete.WithCtrl, defaults[Command.KillWordRight].GetCurrentPlatformKeys ());
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
    ///     Proves that loading a keybinding profile via Microsoft.Extensions.Configuration updates
    ///     the <see cref="Editor" /> instance's key bindings.
    /// </summary>
    /// <remarks>
    ///     The JSON key format is:
    ///     <code>
    ///         {
    ///           "Editor": {
    ///             "DefaultKeyBindings": {
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
    public void MecConfiguration_EditorDefaultKeyBindings_UpdatesEditorBindings ()
    {
        MecEditorSettings original = MecEditorSettings.Defaults;

        try
        {
            IConfiguration configuration = new ConfigurationBuilder ()
                                           .AddInMemoryCollection (new Dictionary<string, string?>
                                           {
                                               ["Editor:DefaultKeyBindings:Cut:All:0"] = "Ctrl+W",
                                               ["Editor:DefaultKeyBindings:Copy:All:0"] = "Ctrl+Shift+C",
                                               ["Editor:DefaultKeyBindings:Undo:All:0"] = "Ctrl+Z"
                                           })
                                           .Build ();

            EditorConfiguration.Apply (configuration);

            // A new Editor must include the configured bindings.
            Editor editor = new ();

            Assert.Contains (Key.W.WithCtrl, editor.KeyBindings.GetAllFromCommands (Command.Cut));
            Assert.Contains (Key.C.WithCtrl.WithShift, editor.KeyBindings.GetAllFromCommands (Command.Copy));
            Assert.Contains (Key.Z.WithCtrl, editor.KeyBindings.GetAllFromCommands (Command.Undo));
        }
        finally
        {
            MecEditorSettings.Defaults = original;
        }
    }
}
