# Make it yours

Open the island’s settings icon or tray menu. The first window is now the simple personalization panel. Changes save after a short pause; the preview responds while a slider moves. Done saves remaining edits and hides the window. Search finds settings across sections, including ordinary words such as eyes, music and brightness. Opening a section or search result starts at the top. Controls use 44-DIP touch targets; sliders can be tapped or dragged. Physical touch testing and a usability study with a young child have not been performed.

## Sections

- Look & feel: session visibility, overall size, resting/open widths, top spacing, clock, screen, glass depth, edge glow, night mode, night dimming and font. Restoring the look preserves permissions, activity choices and device key assignments.
- Eyes: visible pair, pointer-following gaze, natural blinks, happy expressions, size, energy, soft/smooth/quick motion presets, reduced motion, hover opening and fullscreen/maximized tucking. The tucked top edge stays reachable even with ordinary hover opening disabled. Gaze uses each eye pair’s actual location as its reference. No screen contents or camera data are read. With pointer-following and happy reactions both disabled, gaze does not query the pointer.
- Activities: island music visibility, album-cover display, existing Spotify wallpaper, headphone cards, focus/break lengths and battery cards. Direct links open file shelf, notes/tasks and local app shortcuts. Starting over applies the chosen timer length; an in-progress countdown is not silently replaced by editing its default.
- Alerts: volume and brightness display, popup duration, supported brightness-key setup, Windows notification connection, banner/previews toggles, notification duration, Windows banner settings and clearing island entries. Turning off volume/brightness HUDs releases the corresponding key takeover rather than swallowing keys without a popup. Background notifications do not interrupt a system-level control popup. Quiet notification mode still retains inbox entries.
- Privacy & startup: sign-in startup, session clipboard controls, optional weather/network controls, saved-data management and advanced appearance. Windows-managed permissions and data actions are kept explicit; data erasure already uses a confirmation dialog. Startup writes only this account’s Aster Run entry. Startup/permission actions are not presented as undoable local preference edits.

## Safe editing

Preference changes are validated and coalesced before saving. Pending edits are merged with current preferences so another part of Aster cannot be overwritten by an old draft. Undo changes only fields affected by the last local preference edit and preserves newer unrelated permissions/choices. System-managed actions clear that undo affordance. Startup failures restore the switch to the actual registry state. Notification access status is visible in its section. Scope-specific reset preserves notification consent, wallpaper choices, disabled taskbar state and learned device keys.

The preview is an original, local WPF rendering, not a web dashboard. It represents the resting island; activity-specific content is seen in the actual island. Fonts, transparency, eye/clock visibility, widths and night dimming are reflected. Hide lasts for this session; a later launch restores the island as requested. Advanced numeric controls remain available in local tools.

## Modules

`PersonalizationWindow` owns settings presentation, search, touch-sized controls, preview, coalesced edits and explicit action links. `QuietSwitch` provides spring motion with keyboard/automation support. `Preferences.Validated` bounds every new numeric setting. `AppearanceDefaults` applies a presentation-only reset. `PreferenceEdits` merges an undo into current state. Existing AppModel/platform services own media, notification permission, local storage and device control; settings do not duplicate those services.
