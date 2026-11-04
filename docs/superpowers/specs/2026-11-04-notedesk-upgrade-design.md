# NoteDesk 1.1 design

User approved implementation on 2026-11-04 after the grilling interview.

## Product requirements

- macOS-inspired frosted glass, fine borders, soft shadows, blue accent and restrained animation across notes, manager, settings and dialogs. Preserve Windows caption placement and conventions.
- Light, dark and system appearance; default system, change at runtime. Use native Windows backdrop where available; maintain legible opaque fallback with transparency/high contrast disabled or native effects unavailable.
- Global Ctrl+Alt+N creates one blank note per press, focuses its body, works while manager is hidden and app is in tray. Configurable and disableable. Report registration conflicts without losing the previous working shortcut.
- New note initially activated, not permanently topmost; retain per-note pin toggle. Position on pointer's monitor, centered and cascade consecutive notes, clamped to monitor work area. Preserve 350x520 default and existing note geometry.
- Preserve existing notes, tasks, history, reminders, archive, recycle bin, backup, startup setting, local data directory and hidden-not-deleted close behavior. No new runtime dependency.

## Design system

Light: canvas #F5F5F7, glass #EAF1F8, panel #FFFFFF, ink #1D1D1F, muted #626269, accent #0066CC. Dark: canvas #1C1C1E, glass #25282E, panel #2C2C2E, ink #F5F5F7, muted #B4B4BA, accent #0A84FF. Segoe UI / Microsoft YaHei UI fallback; restrained semibold headings, 14px body, small utility labels. Five subtle color families adapted to both themes. Glass sidebar is the signature; content stays calm and readable. No marketing hero or website layout.

## Architecture and acceptance

Keep WPF/.NET Framework 4.x. Separate platform appearance and global-hotkey lifecycle from existing models and windows. Persist optional settings with backward-compatible defaults in the existing schema. Use a message-only native window to receive WM_HOTKEY and unregister on exit; MOD_NOREPEAT avoids key-hold duplicates. Use per-monitor coordinate conversion for note creation. Shared mutable theme brushes update existing controls; refresh cards and note surfaces without rebuilding active note editors. Theme changes must not lose text or keyboard focus.

Build, run isolated model/integration tests, render light/dark/compact screens, inspect native backdrop on screen and test real hotkey focus, conflict, rebinding, disable and disposal using isolated data. Never run tests against the real notes directory. Deliver local executable and source changes; no publication requested.
