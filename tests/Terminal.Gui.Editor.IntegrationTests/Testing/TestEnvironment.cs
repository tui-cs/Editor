using System.Runtime.CompilerServices;

namespace Terminal.Gui.Editor.IntegrationTests.Testing;

/// <summary>
///     Sets <c>DisableRealDriverIO=1</c> before any test runs so the ANSI driver does not attempt
///     real console I/O. Without this, the full integration test suite hangs on local machines
///     (the env var is set in CI via the workflow YAML but was missing for local runs).
/// </summary>
internal static class TestEnvironment
{
    [ModuleInitializer]
    internal static void Init ()
    {
        Environment.SetEnvironmentVariable ("DisableRealDriverIO", "1");
    }
}
