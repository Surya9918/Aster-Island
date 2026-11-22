# Validation and limits

Tested on the user's Microsoft Surface Laptop 7 (Snapdragon/Windows ARM64) on 17 November 2026. The shipped application uses original source and assets. See `artifacts/island-visuals/visual-report.json` and the separate validation ZIP for the final measured values and raw evidence.

## Passed

- Release solution build and self-contained `win-arm64` publish, with zero compiler warnings/errors.
- 65 automated checks: storage recovery and deletion containment; bounded ephemeral clipboard; timer behavior; explicit network denial/consent/revocation and fake transport; analytic spring convergence, interruption, 60/120 Hz numerical consistency; state transitions, notification restoration and simultaneous activity priority.
- Published PE/source audit: 253 PE files accepted as native ARM64 or architecture-neutral IL. No x86/x64 native dependency is accepted. The only app-owned HTTP source is the audited optional weather client.
- Final native process probe reports `Arm64`. Read-only battery/audio/brightness/media-session API probes passed. These probes do not prove playback control works for every player or that brightness setters work on every display.
- The actual retained island was exercised through idle, hover, transition, expanded music/focus/files/note/sound, persistent focus, notification, simultaneous-activity arbitration, hidden, restored and repeated interruption scenarios.
- Own-window state PNGs, seven recorded transition paths, and screenshots at 0.85/1.0/1.2 UI scale. Hidden screenshot is checked for fully transparent pixels.
- Reviewed initial visual baselines and automated comparison for 12 state renders. These establish a regression starting point, not years of cross-device visual stability.
- Live desktop automation of the island: click expansion, Focus activity selection, starting a real timer, Escape collapse with persistent timer, Ctrl+Alt+Space, and explicitly opening the secondary settings window. Normal mode hides the overlay's taskbar entry; the interaction test temporarily exposed it so the automation helper could enumerate it.
- The live island remained visible over the normal application behind it. Transparent outside margins were visible. A short process-endpoint sample contained no app-owned TCP or UDP endpoints.

## Motion measurement

The visual harness records `CompositionTarget.Rendering` callback intervals and update-handler costs in a separate pass without PNG encoding or screen capture. Runs on this machine had a median interval around 16.67 ms and a 95th percentile around 16.7 ms. Exact final numbers are in the JSON report. Rendering tier was 2. This supports approximately 60 Hz callback cadence for this run. It is not GPU-present timing, a minimum-FPS guarantee, or evidence of 120 Hz output. The numerical spring tests cover 120 Hz stepping; actual 120 Hz hardware output remains unverified.

The recorded GIF is made from actual own-window test renders; encoding/capture reduces recording cadence. Its playback frame rate must not be mistaken for the no-capture timing measurement. Screenshot annotation/gallery backgrounds are presentation-only and are not sampled desktop pixels.

Short live-process samples during interaction showed roughly 511–523 MiB working set and about 0.17 CPU seconds added over about eight seconds. This includes shared runtime/graphics pages and is not a sustained power or memory-leak benchmark. No low-memory or all-day battery-life claim is made. Rendering callbacks unsubscribe when settled; notification expiry polling runs only while a notification exists.

## Not verified / not included

- Physical touchscreen and precision-trackpad gestures, pen behavior, alternate monitors, real resolution/refresh changes and mixed-DPI transitions.
- GPU-present frame tracing, 120 Hz output, long-duration battery/thermal/suspend/resume testing, memory-leak soak tests and a full screen-reader audit.
- Exhaustive click-outside behavior across native dialogs, games, task switches and remote desktop; the implementation collapses on window deactivation.
- Live media control across different active players; camera/microphone recording is not implemented.
- Windows notification mirroring, browser hosting, phone remote/sharing, game overlays and taskbar replacement.
- OS backdrop blur/refraction: the current glass is a translucent vector finish, not sampled/blurred desktop content.
- Code signing, installer/MSIX registration, public distribution and independent security certification.

The actual floating island works and has been run and inspected. The unverified items above remain release criteria before calling the application broadly production certified.

## Final measured run

- Composition samples: 649
- Median interval: 16.666 ms
- 95th percentile interval: 16.703 ms
- 99th percentile interval: 16.803 ms
- Mean update handler: 0.053 ms
- Maximum update handler: 1.032 ms
- Intervals above 20 ms: 2

These are callback measurements with the limits described above.

