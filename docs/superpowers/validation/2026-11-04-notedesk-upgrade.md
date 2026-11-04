# NoteDesk 1.1.0 validation

Validated on 2026-11-04 in Windows, using isolated `.qa` and temporary data directories. Real NoteDesk user data was not used.

## Automated checks

Command: `powershell.exe -NoProfile -ExecutionPolicy Bypass -File tests/verify.ps1`

- Build: passed, `dist/NoteDesk.exe`.
- Legacy JSON fields/defaults and existing fields retained: passed.
- Real Win32 shortcut registration, conflict, transactional rebind, disable, invalid combination and disposal: passed.
- Actual keyboard input: blank-note creation, body focus, hidden manager, initial non-topmost state, held-key suppression, rebind and disable: passed.
- Shortcut recorder: entering the existing shortcut does not create an extra note; registration resumes after capture: passed.
- Theme switching preserves editor instance, current text, caret, focus and persisted content: passed.
- Actual WPF window bounds for 320x336 DIP work area, negative-coordinate placement and small-area calculations: passed.
- Opaque undo-toast fallback and matching selected-option foreground: passed.
- Existing persistence, history, restore, atomic backup and reminder model checks: passed.
- Existing headless window/integration checks, timed autoarchive, undo, recycle and archive restore: passed.
- Both themes: all screens rendered, compact note input remains usable and inside bounds, compact manager/settings and long content checks: passed.

The interactive native test reported `effects=True, background alpha=191`, confirming the native backdrop-success path on this system. A native preview window was inspected on the desktop using computer-use. The blue-accent sidebar, normal caption controls, cards and independent note were visible. Final titlebar colors additionally follow appearance.

## Visual inspection

Reviewed light/dark manager, note and settings, compact long-text note, compact settings, reminder, history and reminder-editing dialog. Date picker, editable combo, checkbox and selection appearances were adapted to match the themes. Full-resolution QA images are retained under `.qa/light` and `.qa/dark`.

## Independent review and fixes

Read-only reviewer found three material issues. Each was reproduced by a failing regression before correction: minimum window size overriding a small work area, translucent undo toast when transparency disabled, and selected combo choice foreground under high contrast. All regressions now pass. A further real-key regression caught shortcut recording triggering a duplicate note; this was fixed and verified.

## Remaining environment coverage

Physical multi-monitor mixed DPI, every Windows high-contrast palette and long sleep/resume cycles have not been comprehensively tested. Native acrylic depends on Windows support and system transparency preferences; a solid fallback is provided. No new runtime dependency, no real user-data migration and no GitHub push/release were performed.
