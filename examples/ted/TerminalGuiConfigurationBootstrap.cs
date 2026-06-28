using Terminal.Gui.Configuration;
using Terminal.Gui.Editor.Configuration;

namespace Ted;

internal static class TerminalGuiConfigurationBootstrap
{
    internal static void Apply ()
    {
        TuiConfigurationBuilder builder = new ("ted");
        builder.ApplyToStaticFacades ();
        EditorConfiguration.Apply (builder.Configuration);
        EditorSettings.Apply (builder.Configuration);
    }
}
