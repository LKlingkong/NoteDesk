# NoteDesk 1.2.0 validation

Completed on 2026-11-04 against the approved Q1–Q7 wallpaper/glass design. All test and preview data used isolated `.qa` or temporary directories; the real user notes directory was not used.

## Results

`powershell.exe -NoProfile -ExecutionPolicy Bypass -File tests/verify.ps1` passed after final production changes.

- Build and embedded 3000x1687 wallpaper resource: passed. Source PNG remains unchanged.
- Legacy JSON defaults, aspect-preserving cover and bottom alignment: passed.
- Valid custom import, original-file removal, corrupt/missing/oversized image handling and managed path boundaries: passed.
- Pixel verification of actual blur smoothing, alignment to the sharp wallpaper, per-panel scene sampling, scrolling alignment and full-window coverage on the first layout: passed.
- Shared blur cache bounded to 1280 pixels on its longest axis at 3840x2160 logical scene size: passed.
- Opaque fallback and default secondary text contrast of at least 4.5:1 against the worst default light glass tint: passed.
- Material persistence and reset preserving notes, appearance and shortcut: passed.
- Missing-image reselect refreshes immediately; reimport atomically repairs corrupt managed copies: passed.
- Managed cache cleanup keeps current, notes.json.bak reference and three recent extras; unrelated files retained: passed.
- Real caption button activation: maximize respects the monitor work area, restore, minimize and close-to-tray: passed.
- Existing real-key shortcut tests, body focus, held-key suppression, hidden manager, live appearance/caret retention, rebinding, disable and shortcut recorder: passed.
- Existing persistence, reminders, history, atomic backup and integration/archive/undo/recovery tests: passed.
- All light/dark screens, compact manager/settings, compact notes and long text: passed and screenshots reviewed.

An independent read-only reviewer identified three recovery/storage findings. Each was reproduced in failing tests, corrected and verified green. Additional regressions caught incorrect padded-image scaling, incomplete first layout, insufficient light secondary-text contrast and maximize covering the taskbar; these were also reproduced before correction.

## Desktop inspection

An isolated native preview showed the approved cloud wallpaper behind readable glass islands. Actual mouse double-click on the custom titlebar maximized the manager to the work area. The original independent desktop note kept its Windows-desktop background. While the user interacted with the preview, automatic clicking was stopped; settings state showed live opacity/brightness changes.

## Portable check

Only `NoteDesk.exe` was copied to `.qa/portable-wallpaper`, launched from that directory, and asked to render all screens with isolated data. Exit code 0 and PASS: the wallpaper does not depend on the project assets directory or the attached temporary file. Final assembly version is 1.2.0.0.

## Limits

Physical mixed-DPI multi-monitor setups, every Windows high-contrast palette and long sleep/resume sessions were not comprehensively tested. Low user-selected opacity can reduce readability; the reading-first default is 86%. Custom wallpaper bytes are not in JSON note backups; copy the wallpapers directory or reselect on another machine. No GitHub push or release and no Windows desktop wallpaper change were performed.
