# Spotify atmosphere

Play music in Spotify desktop or its installed web app. Open the island's music page and turn **Spotify atmosphere** on. Settings → Appearance → Desktop integration has the same preference; use Save appearance there. This user's preference is enabled in the installed build.

Each new shared cover becomes a softly blurred, dark atmospheric wallpaper. Pause keeps it. Another current player, missing cover, off or quit restores your previous wallpaper. Changing the wallpaper yourself pauses automatic changes; toggle the mode off/on to resume.

No Spotify account connection, artwork search or additional internet request. Spotify must share cover art with Windows. The installed open.spotify.com web app is supported. An ordinary browser tab with a generic Edge/Chrome media ID is not automatically treated as Spotify. Windows controls fades; no Explorer injection or fixed crossfade-duration guarantee.

Only two generated images are stored. A local journal preserves original image paths, layout/color and available slideshow configuration. Failed restoration keeps recovery data for retry; next startup checks the journal. See PRIVACY.md and GLASS-VALIDATION.md.

Installed Spotify web-app support: the locally observed `open.spotify.com-…!App` media identity is recognized without inspecting tabs or requiring a Spotify account sign-in. Playing activates the atmosphere; a paused session without an existing atmosphere waits for playback.

No Spotify sign-in or permission popup is shown: the mode uses metadata Windows already shares locally. Turning Spotify atmosphere on is the feature opt-in. "Waiting for Spotify playback" means Windows reports the recognized app paused; press Play. If a generic browser is the current player, use the installed Spotify web app or native app.
