# Release Checklist

Use this checklist before creating a public GitHub release.

- App builds successfully in Debug and Release.
- No broken imports or project references.
- No missing assets, icons, or bundled resources.
- No private local paths are included in docs, configs, or release artifacts.
- No passwords, tokens, secrets, server passwords, or private keys are included.
- User data folders are not included in release packages.
- Server saves, downloaded server binaries, logs, backups, SteamCMD files, and mods are excluded.
- README is complete.
- Install instructions are complete.
- Configuration and data-location docs are complete.
- App version is updated using semantic versioning.
- Changelog is updated.
- GitHub release notes are prepared.
- Installer or portable executable is tested.
- Fresh install is tested.
- Portable mode is tested.
- Update process is tested without deleting user data.
- Backup and restore flows are tested.
- Logs are reviewed for startup/runtime errors.
- Release zip checksum is generated.
