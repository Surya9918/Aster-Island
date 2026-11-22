# Aster architecture

The initial v0.1 window plan below is retained as a design record. The delivered UI uses the island-first replacement described in **ISLAND-REDESIGN.md**: the transparent island is the startup window, with retained activity views, an explicit state machine and shared spring clock. The tools/settings window is lazy-created only after an explicit action.

Decision recorded before implementation: an original offline desktop companion, with a deliberately bounded v0.1 feature set. It is not a complete replica of the reference product. See FEATURE-MAP.md for explicit gaps.

## Boundaries

- `Aster.Core`: immutable/validated preferences, timer state, bounded clipboard history, shelf references, network consent policy and audit records; no Windows dependencies.
- `Aster.Infrastructure`: atomic local JSON persistence, fixed-code diagnostics, optional one-shot weather client. HTTP is denied by default, host allowlisted, redirects/cookies disabled, bounded and cancellable. Audit stores host/purpose/outcome/time only.
- `Aster.Platform.Windows`: native Windows media session, battery, audio, brightness and hotkey integrations. No original binary dependencies. COM objects have explicit lifetime ownership.
- `Aster.App`: WPF UI, tray lifetime, overlay and control center, view models, styling and accessibility. Sensitive services only start after in-app consent; the UI never captures camera/microphone audio.
- `Aster.Tests`: dependency-free executable test runner with nonzero exit on failure. Tests use isolated temporary data and fake network transport; platform probes run only when explicitly invoked.

Data flow: Windows event or explicit user action → service → view-model state → WPF binding. Clipboard text stays in memory. Notes/tasks/settings/file references/launcher paths remain under `%LOCALAPPDATA%\AsterDesktop`; v0.1 local JSON is not encrypted. Windows account/filesystem protection is the security boundary. Diagnostic logs never contain exception messages, clipboard contents, notes, coordinates, paths, track names or device identifiers.

## Privacy model

No account, telemetry, purchase code, updater, browser, external fonts, remote artwork, listener, localhost endpoint or background HTTP. Weather is disabled on each process start. Explicit session consent describes the data: manually entered latitude/longitude and the public IP visible to the provider, sent to `api.open-meteo.com` over HTTPS. A separate click fetches once; no scheduled refresh, geocoding or implicit media lookup. Revocation cancels outstanding requests. An audit includes both allowed attempts and blocked requests and can be cleared with all other local data.

Deletion first stops sensitive/network work and suppresses further persistence, then removes only the application's dedicated data directory, rejects reparse-point directories, clears clipboard-history memory and closes the app. It never deletes shelf targets, launcher executables or the Windows clipboard. Backups outside the application and forensic disk erasure are outside scope.

## User experience

Original Aster identity: an abstract four-point star drawn with WPF geometry, system fonts, calm blue/teal accent, rectangular cards and a compact floating capsule. No recovered assets or layouts are copied. The control center is resizable and scrollable; controls expose text labels, tab navigation and at least 36-DIP interactive targets. A capsule provides quick access and media controls. Color/font/radius/opacity/width/layout settings persist. Reduced-motion setting disables capsule entry animation. Theme, accent, text scale and module visibility are independent choices.

## Performance and reliability

ARM64 RID and PlatformTarget are pinned. WPF retained rendering avoids continuous render loops. The clock/timer ticks once per second; battery polls slowly; media uses Windows session events and cancellation/timeouts. Clipboard listens for change events only when enabled. Async work must not block the dispatcher. UI errors show a plain message; fixed-code logs rotate at a small bound. Settings load validates and clamps ranges; writes use an atomic replacement with bounded backup recovery. A single-user session mutex prevents competing instances.

No production-readiness claim will be made solely from a passing build. Manual multi-monitor, touch, long-duration power, suspend/resume, accessibility, and diverse media/device-provider behavior remain release criteria when not directly tested.
