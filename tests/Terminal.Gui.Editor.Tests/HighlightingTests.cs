// CoPilot - claude-opus-4.6

using System.Xml;
using Terminal.Gui.Document;
using Terminal.Gui.Drawing;
using Terminal.Gui.Editor.Rendering;
using Terminal.Gui.Highlighting;
using Terminal.Gui.Highlighting.Xshd;
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
        HighlightingColorizer colorizer = new (highlighter, defaultAttr);

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
        HighlightingColorizer colorizer = new (highlighter, defaultAttr);

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
    public void Colorizer_Uses_Scheme_Code_Role_When_Theme_Defines_It ()
    {
        TextDocument doc = new ("public");
        IHighlightingDefinition csharp = HighlightingManager.Instance.GetDefinition ("C#")!;
        using DocumentHighlighter highlighter = new (doc, csharp);

        Attribute defaultAttr = new (Color.White, Color.Black);
        Attribute themedKeyword = new (Color.Magenta, Color.Black);

        // Single delegate: returns the themed attribute when the scheme explicitly defines the
        // role, null otherwise. One arg is sufficient to fully enable role theming — there is no
        // separate predicate to forget (the resolve/explicit-check footgun is gone).
        HighlightingColorizer colorizer = new (
            highlighter,
            defaultAttr,
            role => role == VisualRole.CodeKeyword ? themedKeyword : null);

        VisualLineBuilder builder = new ();
        DocumentLine line = doc.GetLineByNumber (1);
        VisualLineBuildContext context = new (
            doc, 4, false, defaultAttr, defaultAttr, null, 0, 0, []);

        CellVisualLine visualLine = builder.Build (line, context);
        colorizer.Transform (visualLine);

        // "public" is a keyword → themed CodeKeyword foreground from the scheme.
        Assert.Equal (themedKeyword.Foreground, visualLine.Elements[0].Attribute.Foreground);
    }

    [Fact]
    public void Colorizer_Falls_Back_To_Xshd_When_Theme_Does_Not_Override ()
    {
        TextDocument doc = new ("public");
        IHighlightingDefinition csharp = HighlightingManager.Instance.GetDefinition ("C#")!;
        using DocumentHighlighter highlighter = new (doc, csharp);

        Attribute defaultAttr = new (Color.White, Color.Black);
        Attribute sentinel = new (Color.Magenta, Color.Black);

        // Resolver returns null for every role → scheme defines none explicitly → use xshd's
        // foreground over the editor (scheme) background — never the sentinel themed attribute.
        HighlightingColorizer colorizer = new (
            highlighter,
            defaultAttr,
            _ => null);

        VisualLineBuilder builder = new ();
        DocumentLine line = doc.GetLineByNumber (1);
        VisualLineBuildContext context = new (
            doc, 4, false, defaultAttr, defaultAttr, null, 0, 0, []);

        CellVisualLine visualLine = builder.Build (line, context);
        colorizer.Transform (visualLine);

        Attribute keyword = visualLine.Elements[0].Attribute;
        Assert.NotEqual (sentinel.Foreground, keyword.Foreground);
        Assert.Equal (defaultAttr.Background, keyword.Background);
    }

    [Fact]
    public void XshdRoleMap_Has_Coverage_For_Common_Names ()
    {
        Assert.Equal (VisualRole.CodeKeyword, XshdRoleMap.TryGetRole ("Keyword"));
        Assert.Equal (VisualRole.CodeComment, XshdRoleMap.TryGetRole ("Comment"));
        Assert.Equal (VisualRole.CodeString, XshdRoleMap.TryGetRole ("String"));
        Assert.Equal (VisualRole.CodeNumber, XshdRoleMap.TryGetRole ("Number"));
        Assert.Equal (VisualRole.CodeConstant, XshdRoleMap.TryGetRole ("Bool"));
        Assert.Equal (VisualRole.CodePunctuation, XshdRoleMap.TryGetRole ("Punctuation"));

        // Unmapped names (markdown / one-offs) fall through.
        Assert.Null (XshdRoleMap.TryGetRole ("Heading"));
        Assert.Null (XshdRoleMap.TryGetRole ("DefinitelyNotAColor"));

        // category= wins over the name table; garbage category falls back to the table.
        Assert.Equal (VisualRole.CodeKeyword, XshdRoleMap.ResolveRole ("StringInterpolation", "CodeKeyword"));
        Assert.Equal (VisualRole.CodeString, XshdRoleMap.ResolveRole ("StringInterpolation", null));
        Assert.Equal (VisualRole.CodeComment, XshdRoleMap.ResolveRole ("Comment", "not-a-role"));
        Assert.Null (XshdRoleMap.ResolveRole (null, "not-a-role"));
    }

    [Fact]
    public void Markdown_Definition_Uses_Theme_Roles_For_Hard_To_Read_Default_Colors ()
    {
        IHighlightingDefinition markdown = HighlightingManager.Instance.GetDefinition ("MarkDown")!;

        Assert.Equal (VisualRole.CodeKeyword, markdown.GetNamedColor ("Heading").Role);
        Assert.Equal (VisualRole.CodeComment, markdown.GetNamedColor ("BlockQuote").Role);
        Assert.Equal (VisualRole.CodeIdentifier, markdown.GetNamedColor ("Link").Role);
        Assert.Equal (VisualRole.CodeType, markdown.GetNamedColor ("Image").Role);
    }

    [Fact]
    public void Category_Attribute_Overrides_Table ()
    {
        const string xshd = """
                            <?xml version="1.0"?>
                            <SyntaxDefinition name="CatTest" xmlns="http://icsharpcode.net/sharpdevelop/syntaxdefinition/2008">
                              <Color name="StringInterpolation" category="CodeKeyword" foreground="Red" />
                              <Color name="Comment" foreground="Green" />
                              <RuleSet />
                            </SyntaxDefinition>
                            """;

        using XmlReader reader = XmlReader.Create (new StringReader (xshd));
        IHighlightingDefinition def = HighlightingLoader.Load (reader, null!);

        // category="CodeKeyword" overrides XshdRoleMap's StringInterpolation → CodeString.
        Assert.Equal (VisualRole.CodeKeyword, def.GetNamedColor ("StringInterpolation").Role);

        // No category → built-in table maps Comment → CodeComment.
        Assert.Equal (VisualRole.CodeComment, def.GetNamedColor ("Comment").Role);
    }

    [Fact]
    public void Category_Numeric_String_Is_Not_Treated_As_Role ()
    {
        // Enum.TryParse accepts numeric strings, so category="999" would otherwise parse to
        // the undefined (VisualRole)999 and get passed to the scheme. It must be rejected and
        // fall through to the name table (here: no name → null).
        Assert.Null (XshdRoleMap.ResolveRole (null, "999"));
        Assert.Null (XshdRoleMap.ResolveRole ("DefinitelyNotAColor", "999"));

        // A negative / out-of-range numeric is likewise not a real role.
        Assert.Null (XshdRoleMap.ResolveRole (null, "-1"));

        // The name table still applies when the numeric category is rejected.
        Assert.Equal (VisualRole.CodeKeyword, XshdRoleMap.ResolveRole ("Keyword", "999"));

        // Real role names still win as before.
        Assert.Equal (VisualRole.CodeString, XshdRoleMap.ResolveRole ("Keyword", "CodeString"));
    }

    [Fact]
    public void Anonymous_Category_Only_Color_Materializes_With_Role ()
    {
        // An inline (unnamed) color carrying only category= must still reach the colorizer.
        // Previously VisitColor dropped it because foreground/italic/bold were all null.
        const string xshd = """
                            <?xml version="1.0"?>
                            <SyntaxDefinition name="CatOnly" xmlns="http://icsharpcode.net/sharpdevelop/syntaxdefinition/2008">
                              <RuleSet>
                                <Keywords category="CodeKeyword">
                                  <Word>foo</Word>
                                </Keywords>
                              </RuleSet>
                            </SyntaxDefinition>
                            """;

        using XmlReader reader = XmlReader.Create (new StringReader (xshd));
        IHighlightingDefinition def = HighlightingLoader.Load (reader, null!);

        TextDocument doc = new ("foo bar");
        using DocumentHighlighter highlighter = new (doc, def);
        HighlightedLine highlighted = highlighter.HighlightLine (1);

        var themed = highlighted.Sections.Any (s => s.Color?.Role == VisualRole.CodeKeyword);
        Assert.True (themed,
            "An unnamed <Keywords category=\"CodeKeyword\"> must materialize a color carrying that Role.");
    }

    [Fact]
    public void HighlightingColor_Equality_Considers_Role ()
    {
        // HighlightingEngine.PushColor coalesces adjacent sections when their colors are Equal.
        // Two colors that differ only by Role must NOT be equal, or one token's role would bleed
        // onto the contiguous next token and theme it wrongly.
        HighlightingColor a = new () { Name = "X", Foreground = new HighlightingBrush (Color.Red) };
        HighlightingColor b = new () { Name = "X", Foreground = new HighlightingBrush (Color.Red) };

        Assert.Equal (a, b);

        b.Role = VisualRole.CodeKeyword;

        Assert.NotEqual (a, b);
        Assert.NotEqual (a.GetHashCode (), b.GetHashCode ());

        a.Role = VisualRole.CodeKeyword;
        Assert.Equal (a, b);
        Assert.Equal (a.GetHashCode (), b.GetHashCode ());
    }

    [Fact]
    public void Colorizer_WithDefaultAttribute_Unchanged_Returns_Same_Instance ()
    {
        TextDocument doc = new ("public");
        IHighlightingDefinition csharp = HighlightingManager.Instance.GetDefinition ("C#")!;
        using DocumentHighlighter highlighter = new (doc, csharp);

        Attribute defaultAttr = new (Color.White, Color.Black);
        HighlightingColorizer colorizer = new (highlighter, defaultAttr);

        HighlightingColorizer updated = colorizer.WithDefaultAttribute (defaultAttr);

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
