# Integration test harness — read this before writing render tests

If you are an agent changing anything that affects what the `Editor` **looks like**
(selection, multi-caret, highlighting, tabs, newline glyphs, scrolling, layout): **use ANSI
snapshots.** They let you verify the look yourself, in the loop, without a human eyeballing
the terminal. That is the whole point of this harness.

## TL;DR

```csharp
await using AppFixture<EditorTestHost> fx = new (() => new ("abc\ndef"), width: 10, height: 4);
fx.Top.Editor.SetFocus ();

Inject.AltDrag (fx, new (1, 0), new Point (3, 1));   // mouse gesture
fx.Injector.InjectKey (Key.X, Direct);               // keyboard
fx.Render ();                                        // <-- ALWAYS render last, before Verify

AnsiSnapshot.Verify (fx.Driver, nameof (My_Test));   // records 1st run, compares after
```

First run records `__snapshots__/My_Test.ans` and passes. Later runs compare byte-for-byte.

## The three pieces

| Type | What it is |
|---|---|
| `AppFixture<EditorTestHost>` | Per-test `IApplication` (parallel-safe, no static `Application.Init`). `fx.Driver`, `fx.Injector`, `fx.Render ()`, `fx.Top.Editor`. Slimmer than TG's `AppTestHelper`. |
| `Inject` | Deterministic mouse gestures: `Inject.Click (fx, pt[, modifiers])`, `Inject.AltDrag (fx, press, wp1, wp2, …)`. Fixed monotonic timestamps so a gesture replays identically. |
| `AnsiSnapshot` | Golden-file capture of the screen as **pure ANSI** via `IDriver.ToAnsi ()`. |

Keyboard injection is plain `fx.Injector.InjectKey (key, Direct)` where
`Direct = new InputInjectionOptions { Mode = InputInjectionMode.Direct }`.

## Why ANSI, not a cell/role mask

`IDriver.ToAnsi ()` emits exactly the escape-sequence stream the driver would write to recreate
the screen — fg/bg truecolor, bold, reverse, blink, layout, everything **except the terminal
cursor** (a separate, non-deterministic `SetCursor`, so snapshots stay stable). The recorded
`.ans` file *is* the look:

```sh
cat tests/Terminal.Gui.Editor.IntegrationTests/__snapshots__/My_Test.ans
```

reproduces the exact screen in any truecolor terminal. The primary caret is the terminal
cursor and is **not** in the snapshot; additional carets render as blink+reverse cells
(`ESC[…;5;7m`) and **are** captured.

## The agent loop (no human needed)

1. Run the test. Mismatch → it fails and:
   - prints the **plain-text render inline** in the test log (glyphs only — see the layout immediately, no terminal required), and
   - writes a sibling `__snapshots__/My_Test.ans.actual`.
2. `cat '…/My_Test.ans.actual'` to see the exact new look (colors/styles). Decide if it is correct.
3. If correct, accept it: re-run with `UPDATE_SNAPSHOTS=1` (PowerShell: `$env:UPDATE_SNAPSHOTS=1`),
   or just delete the stale `.ans` and re-run to re-record. Commit the updated golden.
4. If wrong, your change is wrong — fix the code, not the snapshot.

`SNAPSHOT_DIR` overrides the golden root (default: `__snapshots__/` beside the test source).

## Rules that bite (learned the hard way)

- **`fx.Render ()` must be the last thing before `AnsiSnapshot.Verify`.** The snapshot is the
  *driver buffer*, not the document. Inject all input, then render, then verify. A snapshot
  taken before the render that reflects your last edit captures the *previous* frame and will
  silently assert the wrong thing.
- **Goldens are LF-canonical; cross-platform.** TG's `ToAnsi ()` separates rows with
  `StringBuilder.AppendLine ()` == `Environment.NewLine` (CRLF on Windows, LF elsewhere), so a
  golden recorded on one OS would never match another OS's render — this *was* a CI failure.
  `AnsiSnapshot` normalizes both the capture and the file to `\n` before comparing and writes
  goldens LF. `cat` fidelity is unaffected (terminals map LF→CRLF via the ONLCR tty
  discipline). `*.ans` stays `binary` in `.gitattributes` so Windows `core.autocrlf` can't
  reintroduce CRLF on checkout. Don't "fix" a golden by rewriting it with CRLF.
- **Keep goldens compact.** Pass a small `width`/`height` to `AppFixture` (e.g. `10 × 4`) so
  the `.ans` is a few hundred bytes and a human/agent can `cat` it at a glance.
- **`ToAnsi ()` is deterministic** — `EditorSnapshotTests.Snapshot_Render_Is_Deterministic`
  guards this. If it ever flakes, a non-deterministic render leaked in (timestamp, blink phase
  baked into content, GUID); fix that, do not loosen the snapshot.
- **Parallel-safe**: each test owns its `IApplication`. Never `Application.Init ()` (static),
  never `CM.Enable`. See the repo `CLAUDE.md` "Testing tiers".
- **Assert semantics too.** A snapshot proves the *look*; still assert `Document.Text`,
  `CaretOffset`, `HasSelection`, `AdditionalCaretOffsets` for the *meaning*. Look + semantics
  together is what makes these tests trustworthy.

## Examples to copy

- `EditorColumnSelectionTests.cs` — mouse + keyboard gestures, snapshot + state assertions.
- `EditorSnapshotTests.cs` — minimal recorder + the determinism guard.
