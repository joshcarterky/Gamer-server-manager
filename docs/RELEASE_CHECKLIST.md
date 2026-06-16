# Release Checklist

Use this checklist before publishing a GitHub release.

## Before Publishing

- Update app version.
- Build installer.
- Build portable ZIP.
- Test installer.
- Test portable version.
- Test update check.
- Test auto-update download.
- Generate checksums.
- Confirm GitHub release only has clean assets.
- Confirm README download link works.

## Expected Public Assets

- `NexusServerManager-Setup-vX.Y.Z.exe`
- `NexusServerManager-Portable-vX.Y.Z.zip`
- `NexusServerManager-Checksums-vX.Y.Z.txt`

## Updater/Internal Files

Do not upload these as separate public release assets:

- `.nupkg`
- `RELEASES-stable`
- `assets.stable.json`
- `releases.stable.json`
- `update.json`

The release script writes updater internals to `releases/vX.Y.Z/updater-feed/`.
