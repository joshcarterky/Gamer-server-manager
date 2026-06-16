# Release Checklist

Use this checklist before creating a public GitHub release.

## Before Publishing

- Update app version.
- Update `CHANGELOG.md`.
- Update `RELEASE_NOTES.md`.
- Build installer.
- Build portable ZIP.
- Test installer.
- Test portable version.
- Test update check.
- Test auto-update download.
- Generate checksums.
- Confirm GitHub release only has clean assets:
  - `NexusServerManager-Setup-vX.Y.Z.exe`
  - `NexusServerManager-Portable-vX.Y.Z.zip`
  - `NexusServerManager-Checksums-vX.Y.Z.txt`
- Confirm `README.md` download link works.
- Confirm `DOWNLOAD.md` matches the release assets.
- Confirm no private local paths are included in docs, configs, or release artifacts.
- Confirm no passwords, tokens, secrets, server passwords, or private keys are included.
- Confirm user data folders are not included in release packages.
- Confirm server saves, downloaded server binaries, logs, backups, SteamCMD files, and mods are excluded.
- Confirm passwords are masked in diagnostics.
- Push tag `vX.Y.Z`.
- Confirm GitHub Actions release passed.
- Download release files from GitHub.
- Install on a clean Windows machine.
- Verify app starts.
- Verify ARK ASA profile loads.
- Verify Palworld profile loads.
