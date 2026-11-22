# Feature and dependency map

Reference analysis completed before implementation. The complete structural inventory is in REFERENCE-INVENTORY.md and reference-inventory.json. Every archived file was hashed; every C# file was fully read by the analysis script. This does not imply manual semantic verification of every reconstructed statement. Representative implementations, feature enumerations, outbound call sites, manifests, runtime configuration, dependency metadata, and decoded strings were reviewed directly. The 300,406 C# lines contain 22,065 decompiler warnings; generated async state machines inflate counts.

## Reference stack

C#, .NET 10.0.14 self-contained x64 desktop package; WPF windowing, SkiaSharp/HarfBuzz rendering/fonts, Vortice graphics/MediaFoundation, NAudio, ManagedShell taskbar/shell integration, WebView2 browser hosting, QRCoder, Windows SDK/WinRT, and Velopack. HTML/CSS/JavaScript implement the Deck web client. Localization JSON, fonts, images and an icon are embedded. The reconstructed csproj target framework is inaccurate compared with the runtimeconfig. Proprietary names are obfuscated, including an XOR string table. Library presence alone is not evidence of execution.

## Functional inventory

“Planned v0.1” is an implementation target, not a test claim. VALIDATION.md records what actually passed. “Not included” means no inert button or simulated substitute will be exposed.

| Reference feature | OS/dependency requirements | Independent disposition |
|---|---|---|
| Collapsed top overlay, expanded controls, HUD | HWND/topmost, DPI, animation, input | Planned v0.1: original pill + separate resizable control center, keyboard shortcut, tray, animation toggle |
| Themes, fonts, colors, shape, panel layout | WPF/Skia in reference | Planned v0.1: WPF vector UI, configurable theme/accent/font/size/radius/opacity/pill width/layout/module visibility |
| Media metadata, play/pause, previous/next | Windows media sessions | Planned v0.1: Windows media sessions; no remote artwork fetch |
| System volume and microphone mute | Windows Core Audio | Planned v0.1: native COM, explicit changes only |
| Battery, time, power readout | GetSystemPowerStatus | Planned v0.1 |
| Brightness | Laptop display provider | Planned v0.1: Windows WMI provider; explicit unavailable state if unsupported |
| File shelf, drag in/out, selection/clear | Explorer drag-drop, filesystem | Planned v0.1: local references; no automatic upload, file deletion, or directory crawling |
| Clipboard list/filter/pin/snippets | Clipboard listener | Planned v0.1: optional session-only text history, 20-item/15-minute limits, remove/clear; no images or persistence |
| Focus timer, reminders, to-do, notes, calculator | Local state/timing | Planned v0.1: focus timer, local notes and tasks; recurring reminders/calculator not included |
| App launcher | ShellExecute | Planned v0.1: explicit local executable selection, no discovery/crawling or URLs |
| Notifications mirroring | UserNotificationListener and package capability | Not included: portable build has no notification-listener package identity; no fake notifications |
| Weather, location, air quality | Open-Meteo/Nominatim | Planned v0.1: manual coordinates, one-shot weather after per-session consent; no geolocation, reverse lookup or air quality |
| Currency, stock/crypto strip | Frankfurter/Yahoo/CoinGecko | Not included; avoid financial feeds by default |
| Lyrics | LRCLIB with track metadata | Not included; media remains local |
| Pinned sites, web music/video | WebView2 and arbitrary web endpoints | Not included; no embedded browser profile or cookies |
| Twitch/YouTube stream chat | WebSockets/HTTP, possible credentials | Not included |
| File/text transfer to phone | LAN TCP server, short-lived token, QR | Not included pending threat model and pairing tests |
| Deck remote, mouse/keyboard, app actions | LAN TLS, pairing/certificate pinning, SendInput | Not included pending security review; no listening ports |
| AI agent status/notifications and tiles | Loopback HTTP integration | Not included; no localhost server |
| Camera mirror, microphone/audio recording | Camera/MediaFoundation, device permissions | Not included; no capture device is opened |
| Screenshot, snip, screen recording | Capture APIs, encoder, consent | Not included |
| Game overlay/FPS/CPU/GPU strip | Graphics interception/counters, fullscreen behavior | Not included; ordinary topmost UI is not a game overlay |
| Replacement taskbar, tray, shell/task switches | ManagedShell/user32/shell integration | Not included; Windows taskbar untouched |
| Bluetooth battery, Wi-Fi, night light, power mode | Device/provider-specific APIs | Not included; no system-setting shortcuts masquerading as controls |
| Wallpaper/photo/pets | Assets/graphics | Not included; proprietary assets excluded |
| Store purchase/trial/reviews | StoreContext | Independent app has no store, trial, payment or entitlement subsystem; original checks untouched |
| Update checks | Velopack/GitHub source plus Store servicing | No updater in independent app; reference code constructs updater with decoded empty source; runtime behavior not established |

## Reference network map

Decoded strings and HTTP/WebSocket call sites indicate Open-Meteo forecast/geocoding/air-quality; Nominatim reverse geocoding; Frankfurter currency; CoinGecko prices; Yahoo Finance charts; LRCLIB lyrics; Twitch chat; YouTube chat/media; Google search/extensions; Spotify and support/store/marketing links. Arbitrary sites/calendar/provider URLs can add other destinations. Link strings, demo URLs and request destinations are different evidence classes. No recognized Sentry, Application Insights, TelemetryClient, Mixpanel or Amplitude names were found in the scan; this is not proof that custom telemetry is absent.

StoreContext and Velopack appear in reference code. Do not treat licensing calls as telemetry or assume updater code is active in the Store build. Manifest grants full trust, notification listener, location, microphone, webcam and private/domain inbound TCP rule. Actual authorization/use requires separate validation.

Observed original-app connection to 157.90.83.168:443 matched api.open-meteo.com's DNS answer while weather was enabled. TLS payload and hostname were not captured. This supports weather attribution but does not independently certify privacy. Existing evidence is preserved in the original inspection ZIP, outside the new source tree.

## ARM64 risks and decisions

The reference is x64 and includes x64 native graphics/font/audio components. Copying it cannot create native ARM64 support. Aster uses ARM64 .NET/WPF, Windows built-in COM/Win32 APIs and Windows SDK projections only. No third-party native binary, WebView2, Skia, codec, global keyboard hook, graphics injection, or shell replacement is required. Native process architecture and PE headers must be checked after publishing. WMI availability is hardware-specific; unsupported brightness is reported, not simulated.
