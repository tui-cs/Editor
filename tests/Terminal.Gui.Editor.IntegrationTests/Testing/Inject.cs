// Claude - claude-opus-4-7

using System.Drawing;
using Terminal.Gui.Input;
using Terminal.Gui.Testing;

namespace Terminal.Gui.Editor.IntegrationTests.Testing;

/// <summary>
///     Deterministic mouse-gesture injection for editor integration tests. Timestamps are
///     fixed and monotonic so a gesture replays identically every run (snapshot-safe). The
///     per-test private <c>InjectAltDrag</c> copies in <c>EditorMouseTests</c> /
///     <c>EditorMultiCaretIndentTests</c> predate this; new tests should use these.
/// </summary>
public static class Inject
{
    private static readonly InputInjectionOptions Direct = new () { Mode = InputInjectionMode.Direct };
    private static readonly DateTime BaseTime = new (2025, 1, 1, 12, 0, 0);

    /// <summary>Press-then-release left click at <paramref name="pos" />, optionally modified.</summary>
    public static void Click (AppFixture<EditorTestHost> fx, Point pos, MouseFlags modifiers = default)
    {
        ArgumentNullException.ThrowIfNull (fx);

        fx.Injector.InjectMouse (
            new Mouse
            {
                ScreenPosition = pos,
                Flags = MouseFlags.LeftButtonPressed | modifiers,
                Timestamp = BaseTime
            },
            Direct);

        fx.Injector.InjectMouse (
            new Mouse
            {
                ScreenPosition = pos,
                Flags = MouseFlags.LeftButtonReleased,
                Timestamp = BaseTime.AddMilliseconds (20)
            },
            Direct);
    }

    /// <summary>
    ///     <c>Alt</c>+drag: press at <paramref name="press" />, drag through each
    ///     <paramref name="waypoints" /> point (as a held <c>LeftButtonPressed</c>+
    ///     <c>PositionReport</c>), release at the last. Multiple waypoints exercise the
    ///     rebuild-from-scratch column behavior — the end state must equal a single press at the
    ///     final point.
    /// </summary>
    public static void AltDrag (AppFixture<EditorTestHost> fx, Point press, params Point[] waypoints)
    {
        ArgumentNullException.ThrowIfNull (fx);
        ArgumentNullException.ThrowIfNull (waypoints);

        if (waypoints.Length == 0)
        {
            throw new ArgumentException ("Provide at least one drag waypoint.", nameof (waypoints));
        }

        fx.Injector.InjectMouse (
            new Mouse
            {
                ScreenPosition = press,
                Flags = MouseFlags.LeftButtonPressed | MouseFlags.Alt,
                Timestamp = BaseTime
            },
            Direct);

        var t = 1;

        foreach (Point wp in waypoints)
        {
            fx.Injector.InjectMouse (
                new Mouse
                {
                    ScreenPosition = wp,
                    Flags = MouseFlags.LeftButtonPressed | MouseFlags.PositionReport | MouseFlags.Alt,
                    Timestamp = BaseTime.AddMilliseconds (20 * t)
                },
                Direct);
            t++;
        }

        fx.Injector.InjectMouse (
            new Mouse
            {
                ScreenPosition = waypoints[^1],
                Flags = MouseFlags.LeftButtonReleased,
                Timestamp = BaseTime.AddMilliseconds (20 * t)
            },
            Direct);
    }
}
