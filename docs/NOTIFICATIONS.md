# Notification inbox

Native ARM64, original glass UI, grouped by sender application. The bell tab (Ctrl+7) shows up to 100 recent notifications. Expand a card to read, × to hide one, Clear island to hide displayed entries. These actions affect Aster only: Windows Notification Center is preserved. If an application/Windows dismisses a message, its mirrored copy disappears. New arrivals have a small unread count; entering the inbox marks them read.

Windows mirroring is optional. Connect Windows requests access through Windows after the identity package is installed. Disconnect immediately clears mirrored messages. Only the enabled/previews preferences persist. Messages, titles, identifiers and unread state are session memory only; no notification text is logged, written to disk, transmitted, or sent to GitHub. Locking the session clears messages and cached banner text and hides the overlay. Optional weather and unrelated existing features retain their documented permissions.

## Windows banners

The supported listener reads Notification Center; it cannot intercept and cancel other apps’ toast popups. Choose Windows banners in the inbox. In Windows Settings → System → Notifications, open each sender and turn OFF Show notification banners while keeping Show notifications in notification center ON. Do not disable notifications entirely, since that prevents Windows from supplying them. Do not disturb is another Windows option, with priority/alarm exceptions. Aster does not edit undocumented Explorer settings or silently enable Do not disturb. Disabling notification previews in Aster hides message content in transient island banners; deliberately opening Read message still reveals it.

The listener does not expose the original notification’s reply buttons, deep links or activation callback. Aster does not fabricate those controls. Apps that do not publish to Windows Notification Center cannot be mirrored. Initial connection loads existing notifications quietly. Incoming bursts are coalesced into the latest compact banner; all retained messages remain in the inbox. While another expanded activity is in use, arrivals do not replace that activity. Sync checks every two seconds, only while enabled and unlocked, with overlapping queries prevented. This interval means delivery is not instantaneous.

## Identity setup

The sparse MSIX has no app binaries. It grants package identity to the existing ARM64 executable via its embedded manifest. Name: Aster.Island.Desktop; publisher: CN=AsterLocal; app ID: Aster. Capabilities: runFullTrust and unvirtualizedResources for existing desktop/local data behavior, userNotificationListener for explicit notification consent. No network capability is added.

For this laptop a locally generated nonexportable code-signing key signs the package. Its public certificate was explicitly approved for the machine Trusted People store. This is a personal development installation, not a publicly trusted production signing certificate. Do not upload/export the private key. Distribution to other computers requires their informed trust decision or a production signing service. Build the package with Windows SDK MakeAppx, sign with your own code-signing certificate, register using Add-AppxPackage -Path <signed-msix> -ExternalLocation <published-app-directory>. Desktop app.manifest identity must match the package manifest. Assets and packaging manifest live in packaging/identity.

Windows permission can be revoked in Settings → Privacy & security → Notifications. Reconnect after allowing it. Removal of the identity: Get-AppxPackage Aster.Island.Desktop | Remove-AppxPackage (notification access stops; portable local features remain). Remove the specific Aster signing certificate from Trusted People only if no Aster package still needs it. No Windows notification database is accessed directly.

## Validation

109 unit checks passed, including inbox deduplication, bounded bursts, clear/dismiss retention, revocation cleanup, sanitization and transient text clearance. A real WPF harness exercises grouped cards, message expansion, animated dismissal, clear, privacy lock, three scales, repeated/interrupted springs and renders its own isolated fixture images. Separate native integration confirmed actual Windows toast delivery into the real model, Windows removal synchronization and disconnect clearing. Only booleans were recorded from the native test; no personal notification text or screenshots were retained.

Animation timing is measured on this display, not a guarantee of zero dropped frames or 120 FPS. Touch hardware, multi-monitor layouts, long-running battery usage and cross-device behavior still need dedicated validation.

Sources: https://learn.microsoft.com/en-us/windows/apps/develop/notifications/app-notifications/notification-listener and https://learn.microsoft.com/en-us/windows/apps/desktop/modernize/grant-identity-to-nonpackaged-apps .
