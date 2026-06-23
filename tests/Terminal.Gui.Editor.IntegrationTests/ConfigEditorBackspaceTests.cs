using Terminal.Gui.Drivers;
using Terminal.Gui.Editor.Highlighting;
using Terminal.Gui.Editor.IntegrationTests.Testing;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Xunit;

namespace Terminal.Gui.Editor.IntegrationTests;

/// <summary>
///     Characterizes Backspace handling in the shape the winprint TUI uses the <see cref="Editor" /> in:
///     a multi-line JSON document with the <c>Json</c> highlighting definition and a line-number gutter
///     (see winprint's <c>ConfigEditorDialog</c>). Each test injects the <em>raw bytes</em> a terminal
///     sends so the whole driver parse path runs, not a pre-built <see cref="Key" />.
///     <para>
///         Origin: a report that Backspace "did a select-all." It does not — the only select-all is Ctrl+A
///         (<see cref="Backspace_DoesNotSelect_OnlyCtrlA_Does" />). Two real effects can look dramatic:
///     </para>
///     <list type="bullet">
///         <item>
///             The "select all" itself was the <c>Command</c> enum ordinal mismatch (gui-cs/Editor#241):
///             an Editor package built against different Terminal.Gui ordinals re-mapped Backspace's bound
///             command to <c>SelectAll</c>. Fixed by freezing the enum + realigning the package.
///         </item>
///         <item>
///             A terminal that sends the C0 byte <c>0x08</c> (BS / Ctrl+H) word-deletes on Backspace. That
///             is <b>by design</b>: Terminal.Gui decodes <c>0x08</c> as <b>Ctrl+Backspace</b> to provide
///             delete-previous-word in the legacy/ANSI drivers (gui-cs/Terminal.Gui#4099, fixing #4096 /
///             #1211). The kitty keyboard protocol disambiguates plain vs Ctrl Backspace, so it is not an
///             issue on a kitty-capable terminal.
///         </item>
///     </list>
/// </summary>
public class ConfigEditorBackspaceTests
{
    // A representative winprint config: multi-line, indented, JSON.
    private const string Config =
        "{\n  \"DefaultContentType\": \"text/plain\",\n  \"Landscape\": false\n}";

    // Caret inside the "DefaultContentType" key, where a word-kill removes a large span.
    private const int MidKeyCaret = 22;

    private static readonly string ESC = ((char) 0x1b).ToString ();
    private const char DEL = (char) 0x7F; // modern Backspace
    private const char BS = (char) 0x08;  // legacy Backspace / Ctrl+H

    private static AppFixture<EditorTestHost> NewConfigEditor ()
    {
        AppFixture<EditorTestHost> fx = new (() => new EditorTestHost (Config));
        fx.Top.Editor.Multiline = true;
        fx.Top.Editor.GutterOptions = GutterOptions.LineNumbers;
        fx.Top.Editor.HighlightingDefinition = HighlightingManager.Instance.GetDefinition ("Json");
        fx.Top.Editor.SetFocus ();
        fx.Top.Editor.CaretOffset = MidKeyCaret;

        return fx;
    }

    // Feeds raw characters into the driver's input queue and drains the parse pipeline — the path real
    // terminal bytes take, unlike Injector.InjectKey which starts from an already-decoded Key.
    private static void InjectRaw (AppFixture<EditorTestHost> fx, string raw)
    {
        var processor = (InputProcessorImpl<char>) fx.Driver.GetInputProcessor ();

        foreach (char ch in raw)
        {
            processor.InputQueue.Enqueue (ch);
        }

        processor.ProcessQueue ();
    }

    [Fact]
    public async Task Del_0x7F_DeletesSingleChar ()
    {
        // Modern Backspace encoding (DEL). Deletes one grapheme.
        await using AppFixture<EditorTestHost> fx = NewConfigEditor ();

        InjectRaw (fx, DEL.ToString ());

        Assert.Equal (Config.Length - 1, fx.Top.Editor.Document!.Text.Length);
        Assert.False (fx.Top.Editor.HasSelection);
    }

    [Fact]
    public async Task KittyBackspace_DeletesSingleChar ()
    {
        // The kitty keyboard protocol reports plain Backspace as CSI 127 u — the path winprint uses on
        // macOS under Ghostty / kitty. Deletes one char (unambiguous, unaffected by the 0x08 design).
        await using AppFixture<EditorTestHost> fx = NewConfigEditor ();

        InjectRaw (fx, $"{ESC}[127u");

        Assert.Equal (Config.Length - 1, fx.Top.Editor.Document!.Text.Length);
        Assert.False (fx.Top.Editor.HasSelection);
    }

    [Fact]
    public async Task KittyCtrlBackspace_DeletesWord ()
    {
        // kitty reports Ctrl+Backspace as CSI 127;5 u — the unambiguous form of the delete-word gesture
        // (gui-cs/Terminal.Gui#4099). Removes more than one character; no selection.
        await using AppFixture<EditorTestHost> fx = NewConfigEditor ();

        InjectRaw (fx, $"{ESC}[127;5u");

        Assert.True (fx.Top.Editor.Document!.Text.Length < Config.Length - 1);
        Assert.False (fx.Top.Editor.HasSelection);
    }

    [Fact]
    public async Task LegacyBs_0x08_DecodesAsCtrlBackspace_ByDesign ()
    {
        // By design (gui-cs/Terminal.Gui#4099): the legacy BS byte 0x08 is decoded as Ctrl+Backspace so
        // delete-word works in the ANSI/legacy drivers. This documents that intent — NOT a bug. Terminals
        // that emit 0x08 for the plain Backspace key therefore word-delete; kitty avoids the ambiguity.
        await using AppFixture<EditorTestHost> fx = NewConfigEditor ();

        var processor = (InputProcessorImpl<char>) fx.Driver.GetInputProcessor ();
        Key? parsed = null;
        processor.KeyDown += (_, k) => parsed = k;

        processor.InputQueue.Enqueue (BS);
        processor.ProcessQueue ();

        Assert.NotNull (parsed);
        Assert.True (parsed!.IsCtrl);
        Assert.Equal (Key.Backspace, parsed.NoCtrl);
        Assert.True (fx.Top.Editor.Document!.Text.Length < Config.Length - 1); // word removed, not one char
        Assert.False (fx.Top.Editor.HasSelection);
    }

    [Fact]
    public async Task Backspace_DoesNotSelect_OnlyCtrlA_Does ()
    {
        // Disambiguates the "select-all" report: no Backspace byte ever creates a selection. The 0x08 path
        // destroys text (word-delete, above) but never selects; Ctrl+A (0x01) is the only select-all.
        await using AppFixture<EditorTestHost> fx = NewConfigEditor ();

        InjectRaw (fx, BS.ToString ());
        Assert.False (fx.Top.Editor.HasSelection);

        await using AppFixture<EditorTestHost> fx2 = NewConfigEditor ();
        InjectRaw (fx2, ((char) 0x01).ToString ()); // Ctrl+A
        Assert.True (fx2.Top.Editor.HasSelection);
        Assert.Equal (fx2.Top.Editor.Document!.Text.Length, fx2.Top.Editor.SelectedText?.Length);
    }
}
