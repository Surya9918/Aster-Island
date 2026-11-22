# Polish build validation — 22 November 2026

This is a tested polish release, not a claim of defect-free behavior, verified child usability, or an interface impossible to imitate. The repository remains private; no DRM, code obfuscation or invasive anti-copy mechanism was added.

## Current evidence

- Solution build: zero warnings/errors. 113 automated checks passed, including new preference bounds, consent-preserving appearance reset, undo that preserves newer unrelated permission changes, and custom popup duration.
- Actual WPF settings: real eye switch persistence, eye size slider, undo after an unrelated change, search, focus/break lengths, quiet notification inbox, volume-HUD enable/disable, album-art visibility, music activity visibility, notification/system-control priority, nested button drag protection, eyes-only centering, slider touch hit area and the 850 × 640 layout passed. Seven own-window screenshots were captured. These tests use an isolated local profile and do not modify sign-in startup or request permissions.
- Existing island suite: all 22 state checks settled; 21 reviewed visual comparisons passed. Original artwork stream/decoding/clearing tests passed. Notification harness and seven notification visual comparisons passed.
- Native controls: audio and brightness readiness recovery, external volume callback, intercepted volume key, actual level changes, volume HUD, brightness callback/HUD and intercepted Ctrl+Alt brightness shortcut/HUD passed. Original volume/mute/brightness were restored and verified.
- Native Windows notifications: package identity, permission, real Windows toast delivery into the model, removal synchronization and disconnect memory clearing passed. No personal notification content was recorded.
- ARM64/native-source audit: 254 ARM64/neutral PE files, zero audit errors; optional WeatherClient remains the only external HTTP source.
- Cadence: 1,293 repeatedly interrupted transition samples had median 8.3333 ms and p95 8.3919 ms. Maximum measured island update-handler time was 1.1642 ms in that run. Notification harness: median 8.3333 ms, p95 8.3518 ms. These are WPF composition callback measurements, not GPU-present traces. Cold opens, system load and Windows scheduling can still produce slower frames.

## Concrete defects addressed

Appearance reset previously reset unrelated feature preferences. Some media/battery settings did not affect the island. Clock/calendar bindings could lag minute/day changes. File-row action content could start a drag. Double-clicking unrelated activity backgrounds could toggle music. Background notifications could replace a volume/brightness HUD. Turning off auto-stow while an Aster window held focus could leave the previous tuck state. Eye gaze was based on the host-window center rather than the eye pair’s location. These paths were corrected. Existing native audio activation/recovery fixes are retained.

## Scope limits

No reboot/sign-in cycle, physical touchscreen/precision trackpad, physical headphone reconnection, real multi-monitor refresh matrix, long-duration battery/CPU soak, child usability study or GPU present-time trace was performed in this pass. No active Spotify/YouTube player was available for live playback-control testing; local artwork and empty-player handling were exercised. The existing wallpaper renderer/restoration and optional weather services were unchanged in this pass. Their prior validation is historical, not newly repeated here. The source includes their existing native/isolated probes and documented permission controls.

Windows still controls its own notification banners and may display firmware brightness flyouts; the settings describe those limits. The taskbar/dock remains paused and disabled. The new panel improves access to existing local tools; it does not claim new phone, cloud, recording or deep notification-reply functionality.
