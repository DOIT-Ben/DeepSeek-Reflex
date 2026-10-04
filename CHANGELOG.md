# Changelog

## 1.0.8 — 2026-10-05

- Prevent Windows' classic sizing frame from painting over the custom rounded border when opening or returning from settings, or switching active windows. Keep native resizing and minimized activation processing intact.
- Add native edge-pixel checks and repeated real modal settings cycles under default and high DPI, alongside the existing native sizing and taskbar restore regressions.

## 1.0.7 — 2026-10-05

- Restore mouse resizing from the window edges and rounded corners by enabling the native sizing style on the real HWND, including after style updates.
- Keep WinForms' borderless size calculations and the existing custom client frame, avoiding extra native borders or size growth after pinned minimize / restore.
- Add regressions for the actual Windows sizing loop, native sizing style and full client rectangle, alongside the existing taskbar and corner alignment checks.

## 1.0.6 — 2026-10-04

- Native host and shortcuts use the stable `DOITBen.DeepSeekReflex.Windows` Shell identity, distinct from earlier cached entries. Local installation now creates full-brand `DeepSeek-Reflex.lnk` shortcuts, matching the installer, and backs up only verified legacy shortcuts before retiring them.
- Replace the legacy `DeepSeek.exe` 0.1.0 entry point, which still contained the default circle icon, with a small blue-fish launcher for `DeepSeekFloat.exe`.
- Both executable entry points now carry the current brand, version and fish icon. Existing calls to the old filename use the current single-instance window and settings.
- The launcher forwards arguments without corrupting spaces, quotes, Unicode, empty values or trailing backslashes; a real isolated receiver verifies the argument boundary.
- Local upgrades back up and update the old entry point and notify the Shell about it. Installer packages include it, verify both executable icons and versions, and check that neither entry point is busy before upgrading.

## 1.0.5 — 2026-10-04

- Borderless chat retains the native system-menu and minimize styles required for taskbar minimize / restore; the custom caption, rounded frame and cached corner surfaces stay unchanged.
- Process and managed shortcuts explicitly share the unique `DOITBen.DeepSeekReflex` AppUserModelID so Windows can associate the taskbar button with this application.
- Local updates and the installer register the identity on each shortcut through a helper that rejects shortcuts targeting another executable.
- Regressions exercise actual native minimize / restore commands with pinning on and off, verify cached corners after restoration, and verify real shortcut identity persistence without overwriting other shortcut properties.

## 1.0.4 — 2026-10-04

- Transparent blue-fish logo without the exterior white square; retain the approved fish silhouette and internal white artwork.
- ICO export fits the visible alpha bounds with minimal padding and preserves aspect ratio, so the fish occupies more of the tray icon.
- Window and tray load their native system icon sizes separately; local updates notify the Shell about changed application resources and shortcuts.
- Release checks now reject opaque backgrounds, undersized subjects and distorted proportions in every ICO layer. Runtime regressions verify the actual tray bitmap and the window's published taskbar icon.

## 1.0.3 — 2026-10-04

- Full DeepSeek-Reflex brand in the main titlebar, introduction, settings, tray, auxiliary windows and shortcut descriptions.
- Single-instance wake-up uses the updated window title while retaining a fallback for an already running older version. Existing profile and executable paths remain compatible.
- Branding documentation consistently uses DeepSeek-Reflex as the project name. Signing application work does not imply that this release has a trusted signature.

## 1.0.2 — 2026-10-04

- Four-step first-use introduction using the existing smooth frame and animated controls, with skip and replay from Settings > Help.
- Introduction reflects active shortcuts and hide behavior; background startup defers it until an explicit wake. Dismissal is stored separately from existing preferences and website data.
- Hotkeys and composer focus are suspended during the introduction and resume when it closes.
- Bilingual package quick-start instructions use VERSION instead of a stale hard-coded release number.
- Code signing policy and application preparation documented; release files remain unsigned pending provider approval.

## 1.0.1 — 2026-10-04

- Reading preset narrowed from 760 to 752 DIP, with height and website zoom unchanged.
- A darker, DPI-scaled one-DIP outline shared by the main and settings windows; cached antialiased corners and resize batching retained.
- User-approved Reflex plump blue fish with an integrated floating window; body and belly use the same blue in project branding, embedded executable, tray, taskbar and installer.
- Settings and experimental auxiliary windows inherit the application icon; release packaging checks embedded program and installer icon pixels against the shared multi-resolution ICO.
- Branding documentation distinguishes original artwork from third-party trademark clearance. Historical 1.0.0 release assets remain unchanged.

## 1.0.0 — 2026-10-04

First public release as **DeepSeek-Reflex**.

- Official DeepSeek website in a dedicated persistent WebView2 profile.
- Custom centered titlebar, always-on-top toggle, compact / reading sizes and four-edge / four-corner resizing.
- Cached antialiased corner surfaces remain smooth during native window dragging and resizing.
- Configurable global show / hide and selected-text shortcuts; draft-safe import with explicit user confirmation before sending.
- Separate settings panel with animated controls and automatically dismissible tray menu.
- Automatic selection popup disabled by default; experimental implementation retained without an exposed automatic activation entry.
- MIT license, original project logo, Windows x64 installer and standalone ZIP, version metadata and SHA-256 release hashes.
- Offline C# build and repeatable isolated regression checks. Retired Rust/Tauri scaffolding and compiler output are excluded from this public repository.
