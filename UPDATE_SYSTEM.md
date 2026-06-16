# Update System

Nexus Server Manager uses GitHub Releases as the public update source.

## Runtime Flow

1. The app reads the installed version from assembly metadata.
2. Settings > Updates reads the configured GitHub owner and repository.
3. The app queries GitHub Releases, compares semantic versions, and respects Stable/Beta channel settings.
4. The user can download the best Windows asset from the release.
5. Downloads are saved to the app data update downloads folder, not the install folder.
6. `checksums.txt` is used for SHA256 verification when the release includes it.
7. Installer downloads can be launched from inside the app after user confirmation.
8. Velopack package feeds are also generated for installed builds.

## Files

- `VERSION`: source version for release packaging.
- `Directory.Build.props`: assembly version and repository metadata.
- `src/GameServerManager.Services/Updates`: update checks, downloads, logging, history, version parsing, and safety helpers.
- `src/GameServerManager.App/Views/SettingsView.xaml`: Update Manager UI.
- `src/GameServerManager.App/Views/Shell.xaml`: startup update notification banner.

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
- SHA256 verification runs when `checksums.txt` contains a matching file entry.
- Settings are backed up before install actions.
- Update failures are logged with technical details.
