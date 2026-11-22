# Aster Island

A native ARM64 desktop island for Windows: a compact floating pill that expands into local music controls, a focus timer, a file shelf, notes and sound controls. Settings and additional local tools open separately. Original source and vector artwork; no proprietary app binaries or assets are used.


## Make it yours

The gear now opens a simple, searchable settings panel with a live resting-island preview. Look & feel, Eyes, Activities, Alerts and Privacy & startup contain plain-language controls, scoped resets and safe undo. Advanced local tools remain separate. See [personalization](docs/PERSONALIZATION.md) and [current polish validation](docs/POLISH-VALIDATION.md). Current suite: **113 automated checks**; native ARM64 build. The interface is original but is not claimed to be impossible to imitate.

## Living monochrome island · 120 Hz

[Current design and measured performance](docs/LIVE-120HZ.md): two friendly animated eyes, monochrome materials, native headphone connection cards and persistent night mode. Night mode is available in Sound controls, the tray and Appearance. The display must be configured for 120 Hz to target 8.33 ms; exact per-frame timing is not guaranteed.

## Notification inbox

[Notification setup, privacy and Windows banner behavior](docs/NOTIFICATIONS.md). The bell tab (Ctrl+7) shows grouped local and Windows notifications with spring card entrances, expandable text, smooth dismissal and layout reflow. Mirroring requires the separate signed identity package and Windows permission. Taskbar features remain off.

## Current glass update

See [Glass update and Windows limits](docs/GLASS-UPDATE.md) and [current validation](docs/GLASS-VALIDATION.md). The island now has an icon ribbon, vertical device controls, local calendar widgets, maximized/fullscreen auto-stow, standard volume-key takeover, brightness-event HUDs and a reversible experimental Windows taskbar backdrop option.

Hover or tap the thin upper-bar strip to recover the island. Brightness shortcuts are Ctrl+Alt+Up/Down; teach hardware brightness keys in Appearance if they produce interceptable function-key events. Firmware-owned keys can still display Windows' flyout. The actual Windows taskbar is targeted; no replacement dock is installed. Windows controls the fallback taskbar tint, and full Explorer restyling is not claimed.

The earlier build notes below remain useful; the linked current update supersedes any older feature counts or behavior descriptions.

## Run

Extract **Aster-Glass-win-arm64.zip** into a folder you own and open **Aster.App.exe**. Keep all files in the extracted folder together. The self-contained build includes the ARM64 .NET runtime; no separate runtime, administrator access, account, installation, payment or startup registration is required. The first thing shown is the island, not a dashboard.

- Click the pill to expand. Click Music, Focus, Files, Note or Sound to change activity.
- Hover opens the pill by default; this can be disabled in settings. Hold the pill for about half a second to open Sound.
- Escape or the upward chevron collapses. Clicking another window collapses an activated expanded island.
- Ctrl+Alt+Space opens the island; Ctrl+1 through Ctrl+7 switch activity while it has focus.
- Double-click the Music background to play/pause; on Focus/Today it starts or pauses focus. Other activity backgrounds do not control music.
- Drag local files onto the island to add references. Drag a shelf row out to another application.
- Touch handlers support tap, long press, vertical expand/collapse and horizontal activity swipes. Physical touch and precision-trackpad gesture validation remains pending.
- Use the settings icon or tray menu for settings and additional tools. Closing settings hides it. Use the tray's Quit Aster to exit.

The island stays above ordinary windows; it is not an exclusive-fullscreen game overlay. Hide suppresses it for the current session; the tray or shortcut restores it, and a new launch shows it again.

## Customize

The new personalization panel previews and saves island choices. Extra numeric controls are under Privacy & startup → Advanced appearance. Defaults use stiffness 420, damping 38, mass 1. The material is translucent vector glass with specular edge lighting; desktop blur/refraction is not implemented. Monochrome colors and original artwork behavior are documented in the design notes.

## Offline and privacy

No accounts, telemetry, automatic updates, listening ports, remote fonts/artwork or browser engine. Media uses information Windows already exposes. Focus, notes, files, audio and battery work without Internet. Microphone mute changes a device flag and does not record sound.

Optional weather exists only in the secondary tools window. It needs explicit consent each session and an explicit fetch; manually entered coordinates go to `api.open-meteo.com`. There is no background refresh or inferred location. Clipboard history is also optional and session-only. See [PRIVACY.md](docs/PRIVACY.md).

## Build

Install the native Windows ARM64 .NET SDK **10.0.404**, as pinned in global.json. From this directory:

```powershell
./scripts/build.ps1
python ./scripts/verify-artifact.py
```

Or pass the path to a locally unpacked SDK: `./scripts/build.ps1 -Dotnet C:\path\to\dotnet.exe`.

The first restore requires Microsoft/NuGet targeting packages. Later builds can use the populated package cache. The app itself works offline. The build script disables .NET CLI telemetry and uses package lock files. Publish output: `artifacts/Aster-Glass-win-arm64`. No third-party runtime package is declared; Windows SDK projections and Microsoft .NET/WPF runtime files are included by the SDK.

## Tests and visual review

```powershell
dotnet run --project tests/Aster.Tests -c Release
./artifacts/Aster-Glass-win-arm64/Aster.App.exe --visual-test C:\absolute\test-output
python -m pip install -r scripts/requirements-visual.txt
python scripts/visual-regression.py
```

The visual harness uses a fresh temporary profile, no real media metadata and no external requests. It runs all states, captures own-window PNGs, records seven transitions, tests three UI scales, interrupts animations repeatedly, and runs a separate no-capture composition timing pass. `--ui-test` also uses a temporary profile and exposes a taskbar entry solely so desktop automation can enumerate the overlay. Normal launches do not show that entry.

Reviewed baseline PNGs are in `tests/glass-visual-baselines`. Review changed renders before deliberately accepting new baselines with `--accept`. Visual tooling needs Python/Pillow; the application does not.

## Scope and release status

This is a working island-first build, not a complete recreation of every reference feature or a certified production release. Notification presentation includes Aster’s own events and optional Windows Notification Center mirroring. See the notification guide for permission, supported senders and banner limits. Phone remote/sharing, browser hosting, recording, game overlays and taskbar replacement are not included. There are no inert substitutes for those features. See [FEATURE-MAP.md](docs/FEATURE-MAP.md), [VALIDATION.md](docs/VALIDATION.md), and [TROUBLESHOOTING.md](docs/TROUBLESHOOTING.md).

## Source structure

```text
Aster.sln
src/
  Aster.Core/                 state machine, spring physics, timer, privacy/data policies
  Aster.Infrastructure/       local storage, fixed-code logs, optional audited weather
  Aster.Platform.Windows/     native audio, battery, brightness, Windows media sessions
  Aster.App/                  island window, vector glass, retained activity views, settings
tests/
  Aster.Tests/                113 executable automated tests
  visual-baselines/           reviewed own-window renders
scripts/                     build, PE/source audit, visual regression and preview tools
docs/                        inventory, architecture, privacy, validation and troubleshooting
```

Build determinism is enabled and SDK/dependency inputs are pinned. This establishes repeatable build instructions; byte-identical reproduction across different Windows/toolchain environments has not been independently demonstrated. The portable binary is unsigned. Microsoft runtime license/third-party notices remain in the published folder.
