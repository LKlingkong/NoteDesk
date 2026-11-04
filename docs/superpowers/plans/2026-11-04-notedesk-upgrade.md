# NoteDesk upgrade Implementation Plan

> **For agentic workers:** Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox syntax for tracking.

**Goal:** Deliver the approved macOS-inspired appearance and configurable global new-note shortcut.
**Architecture:** Keep the current WPF application. Add focused native-hotkey, appearance and placement helpers; adapt shared controls and settings. Keep old data schema readable with optional defaults.
**Tech Stack:** C#, WPF, .NET Framework 4.x, Win32, system csc, no third-party runtime.
**Spec:** ../specs/2026-11-04-notedesk-upgrade-design.md

## Global Constraints

- Preserve user data and existing behavior; isolate all QA data.
- Default shortcut Ctrl+Alt+N; registration failure preserves previous working shortcut.
- Light/dark/system live appearance; native glass with readable fallback.
- Reuse this fresh checkout; implement on feature branch. User approved starting; continue without another plan approval.

## Review Focus

- Held key, conflict, runtime rebinding, disabled shortcut and process exit release registration.
- Empty legacy JSON with no new fields keeps expected defaults and content.
- Theme switch during unsaved editing preserves text, focus and reminders.
- Negative-coordinate monitor, high DPI and small work area keep new note reachable.
- Glass unavailable, high contrast and transparency disabled remain readable.

### Task 1: Settings, global shortcut and placement

Files: src/DesktopIntegration.cs; src/NoteDesk.cs; build.ps1; tests/UpgradeTests.cs; tests/run.ps1.
Interfaces: Database optional appearance/hotkey fields; GlobalHotkey.TryConfigure(bool,int,int,out string), Dispose(); NotePlacement.Calculate(Rect,Size,int).

- [x] Add isolated tests. `Assert(typeof(Database).GetField("Appearance")!=null,"legacy-compatible appearance default");` must fail on original code.
- [x] Run `powershell -File tests/run.ps1`; expect the appearance-default assertion to fail before implementation.
- [x] Implement settings defaults and native WM_HOTKEY receiver with MOD_NOREPEAT, transactional rebind, error text, disposal; centered cascaded and clamped placement.
- [x] Build and run tests. Validate real key registration ownership and release, monitor calculations and JSON roundtrip.

### Task 2: Appearance and all interface surfaces

Files: src/Appearance.cs; src/NoteDesk.cs; tests/UpgradeTests.cs.
Interfaces: UI.ApplyAppearance(string), UI.IsDark, UI.AppearanceChanged; DesktopGlass.Attach(Window,bool).

- [x] Add test resolving dark appearance and checking visible foreground contrast. Expect absence of application appearance to fail.
- [x] Implement semantic shared brushes and resource-based control styles, native glass/fallback, dark titlebar and contrast handling.
- [x] Update notes, manager, dialogs, settings (appearance and shortcut capture), navigation and icons. Keep active note editor instances during appearance changes.
- [x] Build; run full isolated tests and render both themes and compact screens. Inspect screenshots and fix visible clipping/contrast.

### Task 3: Integration, review and deliverable

Files: tests/UpgradeTests.cs; README.md; RELEASE_NOTES.md; QA artifacts (ignored).

- [x] Test user flow: `SendInput(Ctrl+Alt+N)` creates exactly one blank focused note while manager hidden; rebind, conflict and disable work.
- [x] Run model tests, native integration tests and light/dark rendering with independent data roots. Expected: process exits 0 and PASS result files.
- [x] Obtain fresh code review against approved spec; fix material findings and rerun relevant suite.
- [x] Document appearance, shortcut and compatibility behavior; deliver dist/NoteDesk.exe and QA evidence without pushing or publishing.

## Execution notes

Implemented in the fresh current checkout on feature/macos-ui-global-hotkey. Complete verification and independent review are recorded in ../validation/2026-11-04-notedesk-upgrade.md. Reviewer edge cases and the shortcut recorder bug were reproduced RED then verified GREEN. Source and executable remain local; no remote integration requested.
