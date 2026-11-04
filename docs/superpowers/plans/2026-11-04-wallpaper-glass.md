# Wallpaper glass Implementation Plan

> **For agentic workers:** Use superpowers:executing-plans for continuous execution in this session.

**Goal:** Implement the approved app-owned wallpaper and readable glass islands.
**Architecture:** Keep existing WPF model and note windows. Add managed wallpaper loading, crop geometry, one cached blurred scene, clipped glass surfaces, custom manager chrome and wallpaper preferences.
**Tech Stack:** C#, WPF/.NET Framework 4.x, built-in compiler and image codecs, no new runtime dependency.
**Spec:** ../specs/2026-11-04-wallpaper-glass-design.md

## Global Constraints

- Preserve all 1.1 functions, real user data, and project-installed skill files.
- Use embedded user image unchanged, default alignment .5/1 and 86% opacity/24 DIP blur/85% brightness.
- Separate desktop notes continue sampling Windows desktop.
- No publication, push or Windows wallpaper change.

## Review Focus

- Nonexistent, corrupt, huge and out-of-data-root image paths: safe fallback and unchanged current wallpaper on failed import.
- Resize/scroll/crop changes: glass pixels remain aligned; bounded shared cache and no event-handler leaks.
- Live material controls: retain slider interaction and editor focus; persistence failures remain visible.
- Custom caption: minimize/maximize/restore, work-area bounds, resize and close-to-tray survive.
- Transparency disabled/high contrast: opaque readable panels while shortcuts and note content continue normally.

### Task 1: Model, image resource and validation

Files: src/WallpaperGlass.cs, src/NoteDesk.cs, assets/CloudsWallpaper.png, build.ps1, tests/run.ps1, tests/WallpaperTests.cs.
Interfaces: optional Database wallpaper/material fields; WallpaperFiles.Load/Import; WallpaperGeometry.Cover.

- [x] Write runtime assertions using reflection for missing wallpaper defaults/resource and aspect-fill geometry; run and observe missing-feature failure.
- [x] Add optional settings defaults and unchanged embedded PNG to production/test compiler resources.
- [x] Implement bounded decode, safe managed imports, missing-file fallback and continuous crop alignment.
- [x] Run tests for legacy defaults, exact embedded dimensions, invalid imports, managed-copy independence and crop bounds.

### Task 2: Scene, panels, chrome and preferences

Files: src/WallpaperGlass.cs, src/WallpaperPreferences.cs, src/ManagerChrome.cs, src/NoteDesk.cs, src/Appearance.cs, tests/WallpaperTests.cs.
Interfaces: WallpaperScene.Prepare/RefreshSettings, GlassSurface.Content/Padding, Manager.Wallpaper, DeskApp.UpdateWallpaper.

- [x] Add visual assertions for cached background smoothing/alignment and fallback, plus settings persistence and reset; observe failure on missing scene.
- [x] Implement one bounded blurred bitmap, panel-local clipping/translation, separate crisp content and synchronous offscreen preparation.
- [x] Rebuild manager surface hierarchy into glass islands and own caption. Keep note-window behavior intact.
- [x] Add live controls, copied custom images, position adjustment and reset, saving after a pause without rebuilding active controls.
- [x] Render light/dark/compact settings and long lists; inspect images, update geometry/styles as needed.

### Task 3: Review and deliver

Files: tests/verify.ps1, README.md, RELEASE_NOTES.md, validation report.

- [x] Run existing full suite and new image/material tests, plus actual desktop shortcut flow on the new manager.
- [x] Obtain independent read-only code review, reproduce/fix material findings and rerun suite.
- [x] Package local executable with embedded wallpaper; verify from isolated directory and document image backup boundary.
- [x] Save changes locally and provide executable and theme previews.

## Execution evidence

Completed in the existing checkout. Full verification, independent review findings and fixes, native inspection and single-EXE relocation evidence are recorded in ../validation/2026-11-04-wallpaper-glass.md. Runtime bitmap effects share a bounded cache; transparency permission is cached per settings refresh to avoid per-panel registry reads. No changes to real user notes or Windows wallpaper.
