using System.Reflection;
using Terminal.Gui.App;
using Terminal.Gui.Drivers;

namespace Terminal.Gui.Editor;

internal static class AnsiInputProcessorState
{
    private const string PendingPrintableSuppressionFieldName = "_pendingPrintableSuppression";

    public static void ClearPendingPrintableSuppression (IApplication? app)
    {
        if (app?.Driver?.GetInputProcessor () is not AnsiInputProcessor processor)
        {
            return;
        }

        // Terminal.Gui 2.1.1-develop.98 suppresses the next printable fallback key after parsing
        // ANSI Shift+Tab (ESC [ Z) because Shift+Tab reports Tab as printable text. Clear that
        // one-shot suppression after the editor handles Unindent so the user's next Tab reaches us.
        FieldInfo? field = typeof (AnsiInputProcessor).GetField (
            PendingPrintableSuppressionFieldName,
            BindingFlags.Instance | BindingFlags.NonPublic);

        if (field?.FieldType != typeof (string))
        {
            return;
        }

        field.SetValue (processor, string.Empty);
    }
}
