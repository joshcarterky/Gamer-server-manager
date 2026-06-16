# Update System

Nexus Server Manager uses GitHub Releases as the public update source.

Public releases are intentionally simple. The GitHub release should expose only:

- `NexusServerManager-Setup-vX.Y.Z.exe`
- `NexusServerManager-Portable-vX.Y.Z.zip`
- `NexusServerManager-Checksums-vX.Y.Z.txt`

Velopack feed files are generated into `releases/vX.Y.Z/updater-feed/` for maintainers and are not uploaded as separate public assets.

## Runtime Flow

1. The app reads the installed version from assembly metadata.
2. Settings > Updates reads the configured GitHub owner and repository.
3. The app queries GitHub Releases, compares semantic versions, and respects Stable/Beta channel settings.
4. The user can download the best Windows asset from the release.
5. Downloads are saved to the app data update downloads folder, not the install folder.
6. `NexusServerManager-Checksums-vX.Y.Z.txt` is used for SHA256 verification when the release includes it.
7. Installer downloads can be launched from inside the app after user confirmation.
8. Velopack package feeds are also generated for installed builds.

## Files

- `VERSION`: source version for release packaging.
- `Directory.Build.props`: assembly version and repository metadata.
- `src/GameServerManager.Services/Updates`: update checks, downloads, logging, history, version parsing, and safety helpers.
- `src/GameServerManager.App/Views/SettingsView.xaml`: Update Manager UI.
- `src/GameServerManager.App/Views/Shell.xaml`: startup update notification banner.
- `scripts/release/publish-win-x64.ps1`: builds the setup installer, portable ZIP, checksums, release body, and internal updater feed files.

## Settings

- GitHub owner
- GitHub repository
- Update channel: Stable or Beta
- Check frequency
- Check on startup
- Automatically download updates
- Ask before installing
- Include beta releases

## Logs And History

- `updater.log` is written under the app data logs directory.
- `update-history.json` is written under the app data settings directory.

## Safety

- User server files, backups, profiles, and logs are stored outside the install folder.
- Downloads are verified for existence and non-zero size.
- SHA256 verification runs when the release checksum file contains a matching file entry.
- Settings are backed up before install actions.
- Update failures are logged with technical details.
