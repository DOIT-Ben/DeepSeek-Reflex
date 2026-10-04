# Changelog

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
