// CoPilot - claude-opus-4.6

using Terminal.Gui.Document;
using Terminal.Gui.Drawing;
using Terminal.Gui.Highlighting;
using Terminal.Gui.Editor.Rendering;
using Xunit;
using Attribute = Terminal.Gui.Drawing.Attribute;

namespace Terminal.Gui.Editor.Tests;

/// <summary>
///     Tests for the xshd-based highlighting engine lifted from AvaloniaEdit:
///     <see cref="HighlightingManager" />, <see cref="DocumentHighlighter" />,
///     <see cref="HighlightingColorizer" />, and the <see cref="Editor.HighlightingDefinition" />
///     integration.
/// </summary>
public class HighlightingTests
{
    // ── HighlightingManager ────────────────────────────────────────────────

    [Fact]
    public void Manager_Instance_Is_Not_Null ()
    {
        Assert.NotNull (HighlightingManager.Instance);
    }

    [Fact]
    public void Manager_Has_CSharp_Definition ()
    {
        IHighlightingDefinition? def = HighlightingManager.Instance.GetDefinition ("C#");
        Assert.NotNull (def);
        Assert.Equal ("C#", def.Name);
    }

    [Fact]
    public void Manager_Gets_Definition_By_Extension ()
    {
        IHighlightingDefinition? def = HighlightingManager.Instance.GetDefinitionByExtension (".cs");
        Assert.NotNull (def);
        Assert.Equal ("C#", def.Name);
    }

    [Theory]
    [InlineData (".js", "JavaScript")]
    [InlineData (".html", "HTML")]
    [InlineData (".css", "CSS")]
    [InlineData (".xml", "XML")]
    [InlineData (".json", "Json")]
    [InlineData (".py", "Python")]
    [InlineData (".ps1", "PowerShell")]
    [InlineData (".java", "Java")]
    [InlineData (".sql", "TSQL")]
    [InlineData (".md", "MarkDown")]
    [InlineData (".cpp", "C++")]
    [InlineData (".vb", "VB")]
    public void Manager_Gets_Definition_By_Extension_Various (string ext, string expected)
    {
        IHighlightingDefinition? def = HighlightingManager.Instance.GetDefinitionByExtension (ext);
        Assert.NotNull (def);
        Assert.Equal (expected, def.Name);
    }

    [Fact]
    public void Manager_Returns_Null_For_Unknown_Extension ()
    {
        IHighlightingDefinition? def = HighlightingManager.Instance.GetDefinitionByExtension (".zzz");
        Assert.Null (def);
    }

    [Fact]
    public void Manager_HighlightingDefinitions_Is_Not_Empty ()
    {
        Assert.NotEmpty (HighlightingManager.Instance.HighlightingDefinitions);
    }

    // ── DocumentHighlighter ────────────────────────────────────────────────

    [Fact]
    public void Highlighter_Tokenizes_CSharp_Keyword ()
    {
        TextDocument doc = new ("public class Foo { }");
        IHighlightingDefinition csharp = HighlightingManager.Instance.GetDefinition ("C#")!;
        using DocumentHighlighter highlighter = new (doc, csharp);

        HighlightedLine result = highlighter.HighlightLine (1);

        // "public" should produce at least one highlighted section.
        Assert.NotEmpty (result.Sections);

        // The first section should cover "public" and have a color.
        HighlightedSection firstSection = result.Sections[0];
        Assert.True (firstSection.Length > 0);
        Assert.NotNull (firstSection.Color);
    }

    [Fact]
    public void Highlighter_Tokenizes_CSharp_String_Literal ()
    {
        TextDocument doc = new ("var s = \"hello\";");
        IHighlightingDefinition csharp = HighlightingManager.Instance.GetDefinition ("C#")!;
        using DocumentHighlighter highlighter = new (doc, csharp);

        HighlightedLine result = highlighter.HighlightLine (1);

        // The string literal "hello" should be highlighted.
        Assert.NotEmpty (result.Sections);
    }

    [Fact]
    public void Highlighter_Tokenizes_CSharp_Comment ()
    {
        TextDocument doc = new ("// this is a comment");
        IHighlightingDefinition csharp = HighlightingManager.Instance.GetDefinition ("C#")!;
        using DocumentHighlighter highlighter = new (doc, csharp);

        HighlightedLine result = highlighter.HighlightLine (1);

        // The entire line should be a comment section.
        Assert.NotEmpty (result.Sections);
    }

    [Fact]
    public void Highlighter_Updates_Incrementally_After_Edit ()
    {
        TextDocument doc = new ("public class C { }");
        IHighlightingDefinition csharp = HighlightingManager.Instance.GetDefinition ("C#")!;
        using DocumentHighlighter highlighter = new (doc, csharp);

        // First highlighting pass.
        HighlightedLine initial = highlighter.HighlightLine (1);
        var initialCount = initial.Sections.Count;

        // Edit: insert " // comment" at end of line before closing brace.
        doc.Insert (doc.TextLength, "\n// comment");

        // Re-highlight line 2 (the comment).
        HighlightedLine afterEdit = highlighter.HighlightLine (2);
        Assert.NotEmpty (afterEdit.Sections);
    }

    // ── HighlightingColor ──────────────────────────────────────────────────

    [Fact]
    public void HighlightingColor_RoundTrip_Color_Is_Lossless ()
    {
        Color original = new (0xFF, 0x00, 0x80);
        HighlightingBrush brush = new (original);

        Assert.NotNull (brush.Color);
        Assert.Equal (original.R, brush.Color!.Value.R);
        Assert.Equal (original.G, brush.Color.Value.G);
        Assert.Equal (original.B, brush.Color.Value.B);
    }

    // ── HighlightingColorizer ──────────────────────────────────────────────

    [Fact]
    public void Colorizer_Transforms_Visual_Line_Elements ()
    {
        TextDocument doc = new ("public class Foo { }");
        IHighlightingDefinition csharp = HighlightingManager.Instance.GetDefinition ("C#")!;
        using DocumentHighlighter highlighter = new (doc, csharp);

        Attribute defaultAttr = new (Color.White, Color.Black);
        HighlightingColorizer colorizer = new (highlighter, defaultAttr, true);

        // Build a visual line.
        VisualLineBuilder builder = new ();
        DocumentLine line = doc.GetLineByNumber (1);
        VisualLineBuildContext context = new (
            doc, 4, false, defaultAttr, defaultAttr, null, 0, 0, []);

        CellVisualLine visualLine = builder.Build (line, context);

        // All elements should start with the default attribute.
        foreach (CellVisualLineElement element in visualLine.Elements)
        {
            Assert.Equal (defaultAttr, element.Attribute);
        }

        // Apply the colorizer.
        colorizer.Transform (visualLine);

        // At least one element should now have a different attribute (the keyword "public").
        var hasHighlighted = false;

        foreach (CellVisualLineElement element in visualLine.Elements)
        {
            if (element.Attribute != defaultAttr)
            {
                hasHighlighted = true;

                break;
            }
        }

        Assert.True (hasHighlighted, "Expected at least one element to have a highlighting attribute.");
    }

    [Fact]
    public void Colorizer_Preserves_Default_Attribute_For_Non_Highlighted_Text ()
    {
        TextDocument doc = new ("x");
        IHighlightingDefinition csharp = HighlightingManager.Instance.GetDefinition ("C#")!;
        using DocumentHighlighter highlighter = new (doc, csharp);

        Attribute defaultAttr = new (Color.White, Color.Black);
        HighlightingColorizer colorizer = new (highlighter, defaultAttr, true);

        VisualLineBuilder builder = new ();
        DocumentLine line = doc.GetLineByNumber (1);
        VisualLineBuildContext context = new (
            doc, 4, false, defaultAttr, defaultAttr, null, 0, 0, []);

        CellVisualLine visualLine = builder.Build (line, context);
        colorizer.Transform (visualLine);

        // "x" is not a keyword — should keep the default attribute.
        Assert.Single (visualLine.Elements);
        Assert.Equal (defaultAttr, visualLine.Elements[0].Attribute);
    }

    [Fact]
    public void Colorizer_UseThemeBackground_False_Uses_Default_Background ()
    {
        TextDocument doc = new ("public");
        IHighlightingDefinition csharp = HighlightingManager.Instance.GetDefinition ("C#")!;
        using DocumentHighlighter highlighter = new (doc, csharp);

        Attribute defaultAttr = new (Color.White, Color.Black);
        HighlightingColorizer colorizer = new (highlighter, defaultAttr, false);

        VisualLineBuilder builder = new ();
        DocumentLine line = doc.GetLineByNumber (1);
        VisualLineBuildContext context = new (
            doc, 4, false, defaultAttr, defaultAttr, null, 0, 0, []);

        CellVisualLine visualLine = builder.Build (line, context);
        colorizer.Transform (visualLine);

        // Background should always be the default (Black), regardless of highlighting.
        foreach (CellVisualLineElement element in visualLine.Elements)
        {
            Assert.Equal (Color.Black, element.Attribute.Background);
        }
    }

    [Fact]
    public void Colorizer_WithDefaultAttribute_Unchanged_Returns_Same_Instance ()
    {
        TextDocument doc = new ("public");
        IHighlightingDefinition csharp = HighlightingManager.Instance.GetDefinition ("C#")!;
        using DocumentHighlighter highlighter = new (doc, csharp);

        Attribute defaultAttr = new (Color.White, Color.Black);
        HighlightingColorizer colorizer = new (highlighter, defaultAttr, true);

        HighlightingColorizer updated = colorizer.WithDefaultAttribute (defaultAttr, true);

        Assert.Same (colorizer, updated);
    }

    [Fact]
    public void Highlighter_DefaultTextColor_Uses_Default_Named_Color ()
    {
        TextDocument doc = new ("text");
        HighlightingColor defaultColor = new ()
        {
            Name = "Default",
            Background = new HighlightingBrush (Color.Blue),
            Foreground = new HighlightingBrush (Color.White)
        };
        IHighlightingDefinition definition = new TestHighlightingDefinition (defaultColor);
        using DocumentHighlighter highlighter = new (doc, definition);

        Assert.Same (defaultColor, highlighter.DefaultTextColor);
    }

    // ── Editor.HighlightingDefinition ──────────────────────────────────────

    [Fact]
    public void Editor_HighlightingDefinition_Default_Is_Null ()
    {
        Editor editor = new ();
        Assert.Null (editor.HighlightingDefinition);
    }

    [Fact]
    public void Editor_HighlightingDefinition_Installs_Colorizer ()
    {
        Editor editor = new ();
        editor.Document = new TextDocument ("public class Foo { }");

        editor.HighlightingDefinition = HighlightingManager.Instance.GetDefinition ("C#");

        Assert.Single (editor.LineTransformers);
        Assert.IsType<HighlightingColorizer> (editor.LineTransformers[0]);
    }

    [Fact]
    public void Editor_HighlightingDefinition_Null_Removes_Colorizer ()
    {
        Editor editor = new ();
        editor.Document = new TextDocument ("public class Foo { }");

        editor.HighlightingDefinition = HighlightingManager.Instance.GetDefinition ("C#");
        Assert.Single (editor.LineTransformers);

        editor.HighlightingDefinition = null;
        Assert.Empty (editor.LineTransformers);
    }

    [Fact]
    public void Editor_Switching_Definition_Replaces_Colorizer ()
    {
        Editor editor = new ();
        editor.Document = new TextDocument ("var x = 1;");

        editor.HighlightingDefinition = HighlightingManager.Instance.GetDefinition ("C#");
        IVisualLineTransformer first = editor.LineTransformers[0];

        editor.HighlightingDefinition = HighlightingManager.Instance.GetDefinition ("JavaScript");
        Assert.Single (editor.LineTransformers);
        Assert.NotSame (first, editor.LineTransformers[0]);
    }

    [Fact]
    public void Editor_Document_Change_Reinstalls_Highlighter ()
    {
        Editor editor = new ();
        editor.Document = new TextDocument ("first");
        editor.HighlightingDefinition = HighlightingManager.Instance.GetDefinition ("C#");

        IVisualLineTransformer firstColorizer = editor.LineTransformers[0];

        // Set a new document — the highlighter should be reinstalled for the new document.
        editor.Document = new TextDocument ("second");
        Assert.Single (editor.LineTransformers);
        Assert.NotSame (firstColorizer, editor.LineTransformers[0]);
    }

    private sealed class TestHighlightingDefinition : IHighlightingDefinition
    {
        private readonly HighlightingColor _defaultColor;

        public TestHighlightingDefinition (HighlightingColor defaultColor)
        {
            _defaultColor = defaultColor;
        }

        public string Name => "Test";
        public HighlightingRuleSet MainRuleSet { get; } = new ();
        public IEnumerable<HighlightingColor> NamedHighlightingColors => [_defaultColor];
        public IDictionary<string, string> Properties { get; } = new Dictionary<string, string> ();

        public HighlightingRuleSet GetNamedRuleSet (string name)
        {
            return string.IsNullOrEmpty (name) ? MainRuleSet : null!;
        }

        public HighlightingColor GetNamedColor (string name)
        {
            return name == _defaultColor.Name ? _defaultColor : null!;
        }
    }
}
