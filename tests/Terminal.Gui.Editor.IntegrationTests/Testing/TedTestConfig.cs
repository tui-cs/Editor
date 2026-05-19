namespace Terminal.Gui.Editor.IntegrationTests.Testing;

/// <summary>
///     Supplies a unique throwaway <c>ted.config.json</c> path under the OS temp directory for
///     <c>TedApp</c> construction in tests, so a real <c>TedApp</c> exercising menu/dialog actions
///     persists view settings there instead of polluting the developer's real
///     <c>~/.tui/ted.config.json</c>. Per-instance (passed to the <c>TedApp</c> constructor) — no
///     environment-variable or static mutation, so it stays parallel-safe.
/// </summary>
internal static class TedTestConfig
{
    internal static string NewPath ()
    {
        return Path.Combine (
            Path.GetTempPath (),
            "ted-tests",
            Guid.NewGuid ().ToString ("N"),
            "ted.config.json");
    }
}
