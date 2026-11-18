# Changelog

## v1.1.0 — 2026-11-18

### Added

- Drag one or more PNG, JPG/JPEG or BMP images into the Frames panel to append
  complete, selectable frames. Existing sliced frames and assignments are retained.
- Build packs without a sprite sheet. Individual images support state assignment,
  playback, animation sequences, app icons and all existing export formats.
- Mixed-source exports retain native frame dimensions and transparency; animation
  exports center different frame sizes within a common cell.

### Changed

- Renamed **2 Slice** to **2 Frames** and the frame workspace to **Frames**.
  Sprite-sheet loading remains in **1 Import**, with the same grid workflow.
- Reapplying the grid preserves individually imported frames, assigned roles,
  animation references and icon selection. Importing a new sheet starts fresh.
- Invalid individual images are reported without clearing current work.
  Failed sprite-sheet decoding also preserves the existing session.

### Fixed

- Slice grids extending past the sheet edge no longer silently omit rows or columns.
  The preview marks partial cells red; validation explains the sheet/grid dimensions
  and suggests maximum cell sizes. Invalid settings preserve existing work.
- Frame identity is independent of displayed numbering, so re-slicing cannot
  redirect an imported frame's animation or icon selection.

### Validation and availability

- 37 regression checks passed, including the supplied 944 x 1464 sprite sheet,
  mixed-source PNG/GIF/sprite-sheet/icon exports and import/reset behavior.
- Native Windows single-file and multi-file drop routing checked with a local
  test harness, including drops onto existing tiles and sprite-state assignment.
- Windows x64 ZIP includes .NET and license notices. Extract the complete ZIP and
  run AvatarPackBuilder.exe. The app remains unsigned.
- ImageSharp 3.1.11 dependency advisories remain unresolved. See the README's
  dependency security status; this update does not include the planned migration.

## v1.0.0 — 2026-10-25

- Initial MIT-licensed standalone source and Windows x64 release.
- Sprite-sheet slicing, avatar role assignment, playback and PNG, ICO, GIF and
  animation sprite-sheet exports.
