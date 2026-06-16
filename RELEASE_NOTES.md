# Nexus Server Manager v3.0.5

## Added

- Full update manager in Settings > Updates with a proper state machine (Idle → Checking → UpdateAvailable → Downloading → Downloaded → Installing).
- **Download Update** and **Download and Install** buttons appear only when an update is available and ready to download.
- **Install Update** button appears only after the update has been fully downloaded.
- **Cancel Download** button appears during an active download.
- Green "ready to install" confirmation panel appears when the installer is downloaded and waiting.
- Red warning panel when an update is found but no installer file is attached to the GitHub release.
- Download progress bar and speed indicator are shown only while checking or downloading.
- Checksum verification runs automatically if a checksums file is included in the release.
- Old update downloads older than 30 days are cleaned up automatically on launch.
- Confirm dialog before launching installer clearly states the app will close.
- Install failures return to the Downloaded state so the user can retry without re-downloading.
- Download cancellation returns to the UpdateAvailable state so the user can retry.

## Changed

- Settings > Updates buttons are now state-driven — only relevant actions are shown at each step.
- Download and install are now properly decoupled: download first, then install when ready.
- Update status messages are clearer at each stage of the update flow.
- Settings are automatically backed up before any install is attempted.
- Updater log now records each step of the asset detection, download, and install flow.

## Known Issues

- Code signing is prepared but not configured until a certificate is available.
