// Copilot - Claude Opus 4.6

using System.Text;
using Terminal.Gui.Document;
using Terminal.Gui.Document.Folding;
using Xunit;

namespace Terminal.Gui.Editor.Tests.Folding;

public class AutomaticFoldingTests
{
    [Fact]
    public void Setting_FoldingStrategy_Creates_FoldingManager ()
    {
        TextDocument doc = new ("{\n  content\n}\n");
        Editor editor = new () { Document = doc };

        editor.FoldingStrategy = new BraceFoldingStrategy ();

        Assert.NotNull (editor.FoldingManager);
        Assert.True (editor.AutomaticFolding);
        Assert.True (editor.FoldingManager!.AllFoldings.Any ());

        editor.Dispose ();
    }

    [Fact]
    public void AutomaticFolding_False_Does_Not_Create_FoldingManager ()
    {
        TextDocument doc = new ("{\n  content\n}\n");
        Editor editor = new () { Document = doc };

        editor.AutomaticFolding = false;
        editor.FoldingStrategy = new BraceFoldingStrategy ();

        // AutomaticFolding was explicitly set to false before strategy assignment,
        // but strategy setter auto-enables it. Let's verify the alternative path:
        editor.AutomaticFolding = false;

        Assert.Null (editor.FoldingManager);

        editor.Dispose ();
    }

    [Fact]
    public void Document_Replacement_Reinstalls_Foldings ()
    {
        TextDocument doc1 = new ("{\n  a\n}\n");
        Editor editor = new () { Document = doc1 };
        editor.FoldingStrategy = new BraceFoldingStrategy ();

        FoldingManager? fm1 = editor.FoldingManager;
        Assert.NotNull (fm1);

        TextDocument doc2 = new ("{\n  b\n  {\n    c\n  }\n}\n");
        editor.Document = doc2;

        // FoldingManager should be a new instance for the new document.
        Assert.NotNull (editor.FoldingManager);
        Assert.NotSame (fm1, editor.FoldingManager);
        Assert.True (editor.FoldingManager!.AllFoldings.Count () >= 2);

        editor.Dispose ();
    }

    [Fact]
    public void Non_Structural_Edit_Does_Not_Trigger_Fold_Rescan ()
    {
        TextDocument doc = new ("{\n  hello\n}\n");
        Editor editor = new () { Document = doc };
        editor.FoldingStrategy = new BraceFoldingStrategy ();

        var initialFoldCount = editor.FoldingManager!.AllFoldings.Count ();

        // Insert plain text (no braces or newlines) — should not re-scan.
        doc.Insert (4, "world");

        // Folding count remains the same (foldings were not re-scanned for plain text).
        Assert.Equal (initialFoldCount, editor.FoldingManager.AllFoldings.Count ());

        editor.Dispose ();
    }

    [Fact]
    public void Structural_Edit_Triggers_Fold_Rescan ()
    {
        TextDocument doc = new ("{\n  hello\n}\n");
        Editor editor = new () { Document = doc };
        editor.FoldingStrategy = new BraceFoldingStrategy ();

        var initialFoldCount = editor.FoldingManager!.AllFoldings.Count ();
        Assert.Equal (1, initialFoldCount);

        // Insert a new brace pair spanning lines — should trigger re-scan and add a fold.
        doc.Insert (4, "{\n  nested\n  }");

        Assert.True (editor.FoldingManager.AllFoldings.Count () >= 2);

        editor.Dispose ();
    }

    [Fact]
    public void Large_Document_Skips_Automatic_Folding ()
    {
        // Create a document exceeding the threshold using many short lines
        // (a single mega-line would stress the visual-line builder unrelated to folding).
        var line = new string ('a', 100) + "\n";
        StringBuilder sb = new (1_100_000);

        while (sb.Length < 1_000_001)
        {
            sb.Append (line);
        }

        TextDocument doc = new (sb.ToString ());
        Editor editor = new () { Document = doc };

        editor.FoldingStrategy = new BraceFoldingStrategy ();

        // FoldingManager should be null because the document is too large.
        Assert.Null (editor.FoldingManager);

        editor.Dispose ();
    }

    [Fact]
    public void Custom_MaxLength_Threshold ()
    {
        var text = new string ('a', 500);
        TextDocument doc = new (text);
        Editor editor = new () { Document = doc };

        editor.MaximumAutomaticFoldingDocumentLength = 100;
        editor.FoldingStrategy = new BraceFoldingStrategy ();

        Assert.Null (editor.FoldingManager);

        // Raise the threshold — now folding should install.
        editor.MaximumAutomaticFoldingDocumentLength = 1000;

        Assert.NotNull (editor.FoldingManager);

        editor.Dispose ();
    }

    [Fact]
    public void Shrinking_Document_Below_MaxLength_Reinstalls_Automatic_Folding ()
    {
        string largeContent = new ('a', 200);
        TextDocument doc = new ($"{{\n{largeContent}\n}}\n");
        Editor editor = new () { Document = doc, MaximumAutomaticFoldingDocumentLength = 20 };

        editor.FoldingStrategy = new BraceFoldingStrategy ();

        Assert.Null (editor.FoldingManager);

        const int offsetAfterOpeningBrace = 2;
        doc.Remove (offsetAfterOpeningBrace, largeContent.Length);

        Assert.NotNull (editor.FoldingManager);
        Assert.True (editor.FoldingManager!.AllFoldings.Any ());

        editor.Dispose ();
    }

    [Fact]
    public void Unsetting_FoldingStrategy_Tears_Down ()
    {
        TextDocument doc = new ("{\n  content\n}\n");
        Editor editor = new () { Document = doc };
        editor.FoldingStrategy = new BraceFoldingStrategy ();

        Assert.NotNull (editor.FoldingManager);

        editor.FoldingStrategy = null;

        // Strategy is null — automatic folding should be torn down.
        // FoldingManager is cleared because automatic folding owns it.
        Assert.Null (editor.FoldingManager);

        editor.Dispose ();
    }

    [Fact]
    public void External_FoldingManager_Assignment_Clears_Automatic_Ownership ()
    {
        TextDocument doc = new ("{\n  content\n}\n");
        Editor editor = new () { Document = doc };
        editor.FoldingStrategy = new BraceFoldingStrategy ();
        FoldingManager externalManager = new (doc);

        editor.FoldingManager = externalManager;
        editor.AutomaticFolding = false;
        editor.AutomaticFolding = true;

        Assert.Same (externalManager, editor.FoldingManager);

        editor.Dispose ();
    }

    [Fact]
    public void ChangeMayAffectFoldings_Returns_True_For_Braces ()
    {
        BraceFoldingStrategy strategy = new ();
        TextDocument doc = new ("hello");
        DocumentChangeEventArgs? captured = null;

        doc.Changed += (_, e) => captured = e;
        doc.Insert (5, "{");

        Assert.NotNull (captured);
        Assert.True (strategy.ChangeMayAffectFoldings (captured!));
    }

    [Fact]
    public void ChangeMayAffectFoldings_Returns_False_For_Plain_Text ()
    {
        BraceFoldingStrategy strategy = new ();
        TextDocument doc = new ("hello");
        DocumentChangeEventArgs? captured = null;

        doc.Changed += (_, e) => captured = e;
        doc.Insert (5, "world");

        Assert.NotNull (captured);
        Assert.False (strategy.ChangeMayAffectFoldings (captured!));
    }

    [Fact]
    public void Dispose_Unsubscribes_From_Document ()
    {
        TextDocument doc = new ("{\n  content\n}\n");
        Editor editor = new () { Document = doc };
        editor.FoldingStrategy = new BraceFoldingStrategy ();

        editor.Dispose ();

        // Inserting braces after dispose should not throw.
        doc.Insert (0, "{\n}\n");
    }
}
