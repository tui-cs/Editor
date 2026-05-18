# Feature Specification: Streaming File I/O

**Feature Branch**: `copilot/resolve-open-003-large-file-support`
**Created**: 2026-05-17
**Status**: Implemented
**Input**: Issue #150, TextView parity Gap 6, DEC-001, DEC-009

## User Scenarios & Testing

### Streaming document load

As an editor consumer, I can load a multi-megabyte stream into `TextDocument` without first converting the
entire file to one `string`.

**Acceptance**:

- `TextDocument.LoadAsync (Stream, ...)` decodes in chunks into the rope.
- BOM detection uses `StreamReader` and records the detected `Encoding` on the document.
- Progress reports character count and, when the stream can seek, byte position and total bytes.
- Cancellation is observed between chunks.

### Streaming document save

As an editor consumer, I can save a document to a stream without materializing `Document.Text`.

**Acceptance**:

- `TextDocument.SaveAsync (Stream, ...)` writes a snapshot in chunks using `TextDocument.Encoding`.
- DEC-001 holds: mixed line endings are not normalized, so unedited content round-trips byte-identical for
  the detected encoding/BOM.
- Progress reports characters written and total characters.
- Cancellation is observed between chunks.

### Control-level and ted usage

As a `Terminal.Gui.Editor.Editor` / `ted` user, I can use the streaming path without knowing document internals.

**Acceptance**:

- `Editor.LoadAsync` and `Editor.SaveAsync` delegate to the document APIs.
- `ted` uses stream hooks (`OpenRead`, `CreateWrite`) for File → Open/Save.
- The ted status bar exposes load/save progress and reports completion.
- Menu-triggered opens run asynchronously so the UI can render progress before the full file is loaded.

## API

See [`../public-api.md`](../public-api.md) for the public surface and [`../decisions.md`](../decisions.md)
DEC-009 for placement.
