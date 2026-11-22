# Troubleshooting

- **No visible island:** check the Aster tray menu, select Open island, or press Ctrl+Alt+Space. Check the saved display and top offset in Appearance. A second normal instance reports that Aster is already running.
- **Shortcut unavailable:** another application owns it. Use the tray. Aster does not override another program's hotkey.
- **No music:** open a player that exposes Windows media sessions. No session means controls are unavailable; Aster does not invent track metadata or download artwork. Some players decline previous/next controls.
- **Touch/trackpad differences:** tap/hold/swipe handlers exist, but physical hardware validation is pending. Mouse and keyboard remain usable. Pen and device-specific precision gestures are not certified.
- **Animation feels heavy:** enable Reduce motion, use default spring values, reduce island scale, and keep the default deep glass opacity. Remote Desktop, power modes, capture software and GPU drivers can change WPF cadence. Tier 2 indicates hardware capability, not a 120 FPS guarantee.
- **Text behind glass is distracting:** raise glass opacity to 100%. The material does not blur desktop text.
- **Audio/brightness unavailable:** Windows may expose no suitable provider/device. Brightness depends on the display's WMI provider. Aster reports unavailable instead of simulating a change. Microphone mute affects the default communications input and is not a universal per-application mute guarantee.
- **Settings save failure:** verify access to the Aster data folder. Atomic writes retain one backup. Do not put symbolic links or unrelated subdirectories in that folder.
- **Weather denied:** grant session consent in secondary tools, then enter decimal latitude/longitude and fetch once. Permission is deliberately not remembered across launches. Revoking cannot undo a request already received by a provider.
- **Windows warning for unsigned binary:** this build is unsigned and not Store distributed. No instruction here asks you to bypass a security warning. You can inspect/rebuild the supplied source with Microsoft's pinned ARM64 SDK.
- **Build SDK mismatch:** use SDK 10.0.404 or an allowed servicing patch from global.json. Aster does not install or replace your system SDK. Keep targeting package lock files with the source.
- **Reproduce a visual issue:** run the own-window visual harness, review screenshots/frame timings, then compare against baseline PNGs. Ordinary test runs do not grant network/clipboard permission or change system audio/brightness.

## Current desktop controls

The upper strip is intentional over maximized/fullscreen applications; hover/click it or use Ctrl+Alt+Space. Set Auto-stow off in Appearance to keep the normal pill. Learn brightness function keys in Appearance; firmware-owned keys can still show Windows' HUD. Windows taskbar glass uses a limited compositor backdrop on this build, so tint strength is unavailable and Explorer can retain its own opaque background. The option is off by default and restores captured attributes on normal quit/disable. External monitor brightness without a Windows WMI provider and audio-device hotplug paths are not exhaustively tested; restart Aster if device readouts become stale after changing the default output.
