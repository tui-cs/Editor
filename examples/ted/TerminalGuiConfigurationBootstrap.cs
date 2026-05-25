using System.Reflection;
using Microsoft.Extensions.Configuration;
using Terminal.Gui.Configuration;
using Terminal.Gui.Editor.Configuration;
using ConfigurationManager = Terminal.Gui.Configuration.ConfigurationManager;

namespace Ted;

#pragma warning disable CS0618 // ConfigurationManager is a compatibility fallback for released Terminal.Gui.

internal static class TerminalGuiConfigurationBootstrap
{
    private const string TuiConfigurationBuilderTypeName =
        "Terminal.Gui.Configuration.TuiConfigurationBuilder, Terminal.Gui";

    internal static void Apply ()
    {
        if (TryApplyMec ())
        {
            return;
        }

        ConfigurationManager.Enable (ConfigLocations.All);
        IConfiguration configuration = EditorSettings.BuildConfiguration (EditorSettings.GetConfigPath ());
        EditorConfiguration.Apply (configuration);
        EditorSettings.Apply (configuration);
    }

    private static bool TryApplyMec ()
    {
        Type? builderType = Type.GetType (TuiConfigurationBuilderTypeName, false);

        if (builderType is null)
        {
            return false;
        }

        var builder = Activator.CreateInstance (builderType, "ted")
                      ?? throw new InvalidOperationException ($"Unable to create {builderType.FullName}.");

        MethodInfo applyToStaticFacades = builderType.GetMethod ("ApplyToStaticFacades", Type.EmptyTypes)
                                          ?? throw new MissingMethodException (builderType.FullName,
                                              "ApplyToStaticFacades");

        applyToStaticFacades.Invoke (builder, null);

        PropertyInfo configurationProperty = builderType.GetProperty ("Configuration")
                                             ?? throw new MissingMemberException (builderType.FullName,
                                                 "Configuration");

        if (configurationProperty.GetValue (builder) is not IConfiguration configuration)
        {
            throw new InvalidOperationException (
                $"{builderType.FullName}.Configuration did not return IConfiguration.");
        }

        EditorConfiguration.Apply (configuration);
        EditorSettings.Apply (configuration);

        return true;
    }
}

#pragma warning restore CS0618
