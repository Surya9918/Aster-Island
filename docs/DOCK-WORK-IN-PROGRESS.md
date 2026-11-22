# Custom dock work saved for later

User requested the taskbar feature off on 18 November 2026 to continue other work. Do not resume implementation or enable the dock until asked.

Installed application: the previously published Aster build, with CustomDock=false and TaskbarGlass=false in the user's preferences. Island, Spotify atmosphere and existing startup entry are preserved. No Windhawk installation was performed.

Draft source is saved in DockWindow.cs, DockTests.cs, NativeTaskbar.cs, DockApplications.cs and DockPolicy.cs, plus application/settings wiring. It is not the current published release. Last solution build passed; 96 automated tests passed. artifacts/dock-validation/dock-report.json passed native hide/reveal/re-hide, normal restoration, appbar space reservation, rendered artwork/scales/hover and guardian recovery after terminating only a dedicated helper.

Live UI inspection covered the dock, Start menu, sound/brightness island opening and refined calendar popup. Notification URI launching is implemented but its flyout was not visually confirmed. Physical touchscreen and multi-monitor behavior remain unverified. The recovery shortcut is Ctrl+Alt+Shift+T in draft source; the current installed build predates it.

Before delivery on resume: review viewport width at large scale/many apps (fixed-controls reservation may be underestimated); check native notification access and keyboard recovery; rerun targeted checks after any fixes; publish/package and document the custom dock; enable only with the user's renewed request. Pending taskbar appearance files for Windhawk are superseded by the user's later approval of the original Aster dock.
