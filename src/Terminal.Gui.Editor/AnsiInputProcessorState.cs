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
