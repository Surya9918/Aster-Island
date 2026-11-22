# Glass and desktop-integration update

This update uses the user's reference images for layout direction, with original vector icons and implementation. The top icon ribbon opens six retained pages: Today, Music, Focus, Files, Note and Sound. Sound contains two vertical touch/keyboard-accessible glass faders and functional Wi-Fi, Bluetooth, focus, mute, battery and Windows-taskbar controls. Radio changes require Windows authorization; a denial is reported instead of showing a fake toggle. Today uses local date/week/time, battery and the actual focus timer. It does not display invented weather values.

## Upper-bar behavior

With **Tuck into the top bar for maximized and fullscreen apps** enabled, a foreign maximized/fullscreen window on the island's display moves the island partly above the physical screen edge. Only a 2-DIP lip remains (before user scaling). A centered 296-DIP-wide reveal zone touches the very top pixel: move the pointer straight up to it to open the island with the existing interruptible spring motion. There is no dwell timer or click requirement. Leaving a hover-opened island collapses it after a 140 ms exit grace period; clicking/touching its controls keeps it open. Temporary volume/brightness HUDs take priority over edge hover.

The idle pill is now 224×40 DIP, with a 0.9 base scale applied to all states (about 202×36 at the default preference). All island text is white; primary buttons use darker surfaces for contrast. The settings scale control remains available. The open-island inset affects the visible surface, while the host and reveal zone always remain at the physical display top.

Normal windows or the desktop restore the compact island. Minimized windows are excluded from the visible-application scan; when none remain on the island's display, stale maximized/own-window focus cannot keep the island stowed. Ongoing focus/media activities still use their compact activity pill. Own settings, shell/tool/cloaked windows and windows on another display are excluded from the scan. This reads local window geometry/process IDs only; no titles or contents are recorded.

## Volume and brightness indicators

- Standard volume up/down/mute virtual keys are intercepted on the local desktop and applied through Core Audio. When the device is available, Aster consumes the key event before the shell handles it, and shows a spring-animated island HUD. A native input probe confirmed interception, a real volume change and the island HUD; the test restored the original level.
- Core Audio callbacks also observe external changes to the current output endpoint. Windows WMI brightness events update brightness independently of Aster's own controls. The native probe confirmed both event paths, actual brightness changes and the brightness HUD, then restored the original levels.
- Ctrl+Alt+Up/Down adjusts brightness through Aster. Optional **Teach brightness down/up** captures an explicitly chosen function/OEM key; ordinary text keys are not eligible and are never stored. Learning expires after 12 seconds.
- Laptop firmware/HID keys do not always produce interceptable keyboard events. Those keys can still trigger Windows' flyout. There is no supported global shell-flyout off switch implemented here. Full suppression of every firmware or elevated-app event is **not claimed**.
- The hook does not log typing. Unrelated events are immediately forwarded. If a device operation fails, its keys fall back to Windows. Explicitly hiding Aster also stops Aster from consuming these keys. Quitting removes the hook.
- While Sound is already expanded, changes update the faders/footer in place rather than interrupting the controls with a compact HUD.

## Windows taskbar, not a substitute dock

The taskbar control requests a material on the actual Windows shell taskbar. No separate Aster dock, Explorer injection, patched system files or registry theme edits are used. It first attempts a restorable native accent and otherwise uses Windows 11's DWM transient backdrop. Original values are restored on normal quit/disable, including when Windows ends the app session.

On this laptop's Windows 11 build 26300, the accent route was declined; the DWM backdrop route was accepted and restoration succeeded. **Windows chooses the final tint and can continue painting an opaque Explorer/XAML background.** This is an experimental backdrop option, not a verified full NotchPixel-style taskbar replacement. The tint slider is disabled when the active API cannot control it. Do not interpret API acceptance as a visual certification or run multiple taskbar stylers expecting them to agree.

## Material and validation

The island has softer glass tiles, stronger specular lighting, rounded faders and spring-driven indicators. A native acrylic-underlay experiment produced visible artifacts on this Windows build and was removed. The shipped island material is the tested translucent vector finish; it does not capture, blur or refract desktop pixels. The Windows taskbar backdrop is a separate OS-owned effect.

The test suite has 83 checks, including the new foreground policy, stow/peek/HUD restoration, media-key whitelist and preference bounds. The native device probe passed real maximized-window stowing, external volume callbacks, volume-key takeover, brightness event observation and HUD presentation. Physical Surface brightness-key learning, elevated-app interception, 120 Hz presentation, cross-monitor hotplug behavior and a complete Explorer taskbar redesign remain unverified or outside this update's scope.

Windows API references: [keyboard hook handling](https://learn.microsoft.com/en-us/windows/win32/winmsg/lowlevelkeyboardproc), [Core Audio notifications](https://learn.microsoft.com/en-us/windows/win32/api/endpointvolume/nn-endpointvolume-iaudioendpointvolumecallback), [brightness events](https://learn.microsoft.com/en-us/windows/win32/wmicoreprov/wmimonitorbrightnessevent), [DWM backdrop types](https://learn.microsoft.com/en-us/windows/win32/api/dwmapi/ne-dwmapi-dwm_systembackdrop_type).

## Current media cover art update

The compact pill replaces the Aster mark with the current player-provided cover art (including video thumbnails when the browser publishes them). Music's expanded page uses the same cover, rounded and cropped to fill its existing artwork tile. Artwork follows media-session/track changes, including paused items while they remain the current session. A new item without artwork clears the previous cover and shows the original icon. No independent web-artwork search or download service was added. A browser/player must supply a thumbnail through Windows; Aster cannot manufacture a missing YouTube thumbnail.

The artwork integration test reads an original local fixture through the real WinRT thumbnail-stream API, decodes it, checks colored artwork pixels in actual compact and expanded WPF renders, rejects oversized/malformed/null input, and verifies old artwork clears. This fixture does not certify every streaming player/browser combination.

## Coordinated spring motion update

The island now uses separate physical response profiles for opening and closing. Opening lets width lead height slightly, with a small damped overshoot; contraction uses faster, near-critical damping to avoid a rubbery bounce. Corner shape/top position and opacity have coordinated responses. Content has its own spring, geometry-gated reveal, subtle scale and vertical offset, so the surface opens before text becomes readable. Retargeting never resets spring velocity or position. Reduced Motion still snaps to the final state.

At default user spring settings, opening width has damping ratio about 0.78 and height about 0.83; closing is approximately critically damped. These are original Aster parameters, inspired by the feel of iPhone motion, not Apple's private animation implementation. Existing stiffness/damping/mass controls remain active. Four additional tests cover controlled overshoot, fast useful-size arrival, interruption continuity, and identical analytic trajectories at 60/120 Hz. Actual display timing remains measured separately; a 120 FPS presentation guarantee is not claimed.

## Hover to open

Hovering anywhere on the compact pill or its tucked top-edge reveal zone now expands the island immediately with the coordinated springs. Moving away collapses a hover-opened island after a 140 ms grace period. Clicking/touching a control keeps the island open. Hover does not take keyboard focus or interrupt temporary notifications/volume/brightness HUDs. The existing current activity page is retained.

## Spotify atmosphere

Music has a Spotify atmosphere switch for soft blurred cover art with a dark gradient. Rendering runs off the UI thread only on artwork changes, at 1600×1000 with edge-safe blur, vignette and subtle grain. Rapid changes coalesce and duplicate covers are skipped. Island springs, top-edge/hover reveal and hardware controls remain. Windows owns desktop composition and transition effects. Usage/restoration: SPOTIFY-WALLPAPER.md.

Installed Spotify web-app support: the locally observed `open.spotify.com-…!App` media identity is recognized without inspecting tabs or requiring a Spotify account sign-in. Playing activates the atmosphere; a paused session without an existing atmosphere waits for playback.
