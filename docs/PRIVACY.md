# Privacy and external connection audit

## Default behavior

All Aster functionality starts offline. No account, analytics, telemetry, updater, embedded browser, independent artwork downloader or network listener exists in the source. Optional clipboard history is off on each launch and stores at most 20 text entries, for 15 minutes, in memory only. Disabling clears it. The Windows clipboard is not erased. Clipboard providers that explicitly mark excluded content are skipped, but not every password manager marks its content; users should keep history disabled when handling secrets.

Local notes/tasks/preferences/file references/launcher paths are stored under `%LOCALAPPDATA%\AsterDesktop`. JSON is not encrypted; Windows account and filesystem access are the security boundary. Fixed-code diagnostics exclude personal paths, clipboard/note text, track metadata, coordinates and exception messages. The delete-data command stops sensitive/network work, deletes only the dedicated app data directory, and quits. It refuses unexpected nested directories/reparse points and never deletes shelf targets. This is logical deletion, not forensic erasure.

## Complete app-owned HTTP map

| Destination | Data | Trigger | Controls |
|---|---|---|---|
| `https://api.open-meteo.com/v1/forecast` | Manually entered latitude/longitude rounded to four decimals and requested weather fields; provider necessarily sees public IP and transport metadata | Explicit session permission followed by Fetch weather once in secondary tools | Host/path/scheme/port allowlist; cookies/redirects/default credentials off; timeout 12 s; response buffer bounded; permission revocation cancels request |

No other application HTTP client, WebSocket, TCP listener or UDP client is present in the source audit. The optional weather audit is bounded to 100 session-only entries containing UTC time, approved host, fixed purpose and outcome. It does not log query strings or coordinates. Blocking at Aster's client boundary is not an operating-system firewall for external programs. Opening local files or launchers hands control to Windows/other apps, whose network behavior is outside this audit. OS certificate validation, DNS and Windows services can have their own network behavior.

Tests use an in-memory fake HTTP transport, never a live weather request. The live isolated island process was sampled with no app-owned TCP or UDP endpoints. That short observation is evidence for the tested idle/interaction path, not proof of every OS-level network operation. No packet payload interception or user browser/cookie inspection was performed.

## Reference separation

The full recovered inventory stays documented as reference evidence. The independent source/build has no reference app assemblies, assets, store entitlements, trial checks, update mechanisms or branding. No original license protection was modified. The original application's optional connections are mapped in FEATURE-MAP.md; those destinations are not silently inherited by Aster.

## Added desktop controls

The current update locally checks foreground window geometry/class/process ownership to auto-stow. It does not read or persist window titles/content. A low-level keyboard hook acts only on standard media keys, explicitly learned non-text brightness keys, and the documented brightness shortcut. No typing log exists. Windows audio/brightness event subscriptions update local readouts; Wi-Fi/Bluetooth controls use Windows authorization and read radio state, not network names. The taskbar option changes captured native compositor attributes and restores them on normal quit/disable; no Explorer injection, system-file patching or registry theme modification is used. See GLASS-UPDATE.md for suppression and restoration limits.

## Player-provided cover art

Aster reads the current Windows media session's Thumbnail stream. It does not search artwork websites, construct thumbnail URLs, inspect browser tabs or call YouTube APIs. The player or Windows may obtain that image using its own network behavior; that is outside Aster's HTTP audit. Encoded images are limited to 4 MiB, reads time out after three seconds, and decoding is bounded to 256×256 pixels off the UI thread. Cover images remain in memory and are replaced/cleared on media updates; metadata is never logged. Enabled Spotify wallpaper mode separately writes two processed blurred backgrounds and a restoration journal locally. Missing, failed or malformed artwork uses the original local icon. Rapid updates coalesce and trigger a follow-up refresh rather than queuing unbounded reads.

Reference: [Windows media Thumbnail stream](https://learn.microsoft.com/en-us/uwp/api/windows.media.control.globalsystemmediatransportcontrolssessionmediaproperties.thumbnail).

## Optional Spotify atmosphere

Off by default in new profiles. Enabled here at the user's request. Uses only the current Windows media session's Spotify desktop/Store/installed-web-app source ID and shared cover. No browser-tab inspection, Spotify sign-in/tokens, microphone/loopback monitoring or Spotify HTTP API. Backgrounds are generated once per distinct cover, with a 450 ms debounce; no continuous visualizer runs.

`wallpaper-a.png` and `wallpaper-b.png` are a bounded processed-image cache. `wallpaper-restore.json` contains original wallpaper paths per monitor, layout/color and available slideshow files/options. Paths remain local and are excluded from diagnostics. Off, quit, another current player, missing artwork or deleting Aster data restores the wallpaper still owned by Aster and clears the cache. Failed restoration retains the journal/cache for retry and startup recovery. A manual wallpaper change takes precedence and suspends changes until the mode is toggled off/on.

No new Aster network client was added. The player and Windows can have their own network behavior. [Windows IDesktopWallpaper](https://learn.microsoft.com/en-us/windows/win32/api/shobjidl_core/nn-shobjidl_core-idesktopwallpaper) controls desktop composition and transition effects. Multi-monitor hotplug, Spotlight scheduling restoration and third-party animated wallpaper engines are not certified.

Installed Spotify web-app support: the locally observed `open.spotify.com-…!App` media identity is recognized without inspecting tabs or requiring a Spotify account sign-in. Playing activates the atmosphere; a paused session without an existing atmosphere waits for playback.
