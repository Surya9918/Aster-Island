# Current ARM64 motion validation

- Release build: zero warnings/errors. 93/93 automated checks pass.
- 21 visual comparisons pass against reviewed baselines, including cover art and the fully transparent Hidden state.
- Opening/closing transition frames were recorded and inspected. Repeated interrupted transitions settle correctly. Original cover art, compact sizing and physical top-edge reveal remain in place.
- Four motion checks verify useful opening size within 250 ms, bounded overshoot, retained position/velocity when interrupted, and equivalent analytic trajectories at 60/120 Hz. This is original iPhone-inspired motion, not a claim to reproduce Apple's proprietary parameters.
- The Windows media thumbnail fixture passes actual stream roundtrip, compact/expanded rendered pixels, bounded frozen decoding, malformed/null/oversized fallback and clearing old art. Live YouTube thumbnail availability depends on the browser/player and was not exercised.
- Native device probe passes maximized foreground stow, external volume callbacks, intercepted volume keys, actual volume/brightness changes and both HUDs. Original levels are restored after testing.
- The last-app-minimized condition is tested at the state/policy level; other user applications were not all minimized during testing.
- Architecture audit accepts all 254 ARM64/neutral managed-IL binaries. Only the declared optional weather HTTP client exists.

Latest no-capture WPF composition callback measurement: 647 samples; median 16.666 ms, p95 16.777 ms, p99 16.826 ms. Mean update handler 0.052 ms; maximum 0.278 ms; 3 intervals above 20 ms. These are callback timings, not GPU presentation FPS or proof of 120 Hz output. Recorded GIFs include capture/encoding overhead.

Physical touch/trackpad hardware, mixed-DPI hotplug, long-duration leak/power tests and 120 Hz presentation remain unverified. Taskbar styling remains an experimental reversible Windows backdrop request, with Windows-controlled tint; firmware brightness flyout suppression is not guaranteed. The island uses translucent vector glass without desktop capture/refraction.

## Hover opening validation

The actual compact-island MouseEnter handler is exercised through a routed WPF input event. It opens the retained island without a click, and the resulting expanded frame is captured and inspected. A second hover during a volume HUD preserves that HUD. Existing leave behavior retains a 140 ms grace period, preventing collapse while moving into the expanded controls. This pass uses the real input handler, but does not certify every physical mouse/touch device.

## Spotify atmosphere validation

The wallpaper integration report passes: Spotify/Store source recognition, actual controller-generated image application, duplicate-cover suppression, next-cover change, pause retention, restoring on another player/missing cover/off/quit, preserving manual wallpaper changes across unrelated appearance changes, cache cleanup, crash-journal recovery, shutdown during an update and retry after simulated restore failure. The actual Windows wallpaper API was also exercised on this laptop: one monitor accepted the generated background, then its original image path, layout and background color were verified restored.

The 1600×1000 local fixture rendered in roughly 21 ms on this ARM64 laptop, once per cover change. This excludes Windows desktop composition and is not a crossfade FPS measurement. Original fixture output is Aster-Spotify-wallpaper-preview.png. Live Spotify track switching was not exercised; the current session read by Windows belonged to another player and supplied artwork. Windows controls desktop fading. Multi-monitor hotplug, Spotlight scheduling, slideshow-library recovery and third-party wallpaper-engine coexistence remain unverified.

Final wallpaper release composition measurement: 642 samples, median 16.666 ms, p95 16.796 ms, p99 20.553 ms. Handler mean 0.049 ms, max 0.281 ms. These supersede the earlier callback measurement; they are not desktop-wallpaper crossfade FPS. All 21 reviewed visual comparisons and 93 automated tests pass.

## Spotify installed web-app detection fix

The Windows media-session probe found this laptop's installed Spotify web app publishing `open.spotify.com-D55111A4_nwqr74yvdh6rj!App`, with shared artwork. The previous desktop/Store-only source filter excluded it. The updated bounded host/package-ID pattern now recognizes that source; the live read-only probe confirms Spotify recognition and shared artwork. Windows reported it paused during verification. Two regression checks cover the installed-web-app identity and reject generic browsers/lookalike hosts. Wallpaper-controller and native restoration tests were rerun. No browser tabs were inspected and no Spotify account authorization was added.
