# Changelog

All notable changes to Nexus Server Manager will be documented in this file.

This project follows semantic versioning.

## [Unreleased]

### Added

### Changed

### Fixed

## [3.1.0] - 2026-06-17

### Added

- Added dedicated ARK ASA category content for Health and Validation, Install / Update, Startup, and Raw INI Editor.
- Added regression coverage for ARK selected-category rendering, collapsed technical previews, masked raw editor bindings, and Basic/Advanced mode selection.

### Changed

- ARK ASA Basic and Advanced mode selection now uses one mutually exclusive radio group.
- ARK ASA navigation count now says "available settings" unless search is active.
- Raw INI editing is now isolated under Advanced > Raw INI Editor with file tabs and masked sensitive display by default.

### Fixed

- Fixed ARK ASA technical panels being rendered beneath every selected category.
- Moved SteamCMD command preview to Install / Update, launch command preview to Startup, configuration diff to Health and Validation, and full INI editors to Raw INI Editor.
- Masked sensitive ARK values in command previews, configuration diffs, and raw INI editor display unless reveal is explicitly enabled.
- Raw sensitive values now re-mask automatically when leaving Raw INI Editor.

## [3.0.9] - 2026-06-17

### Added

- Added staged updater downloads under version-specific update folders with `.partial` files.
- Added package verification for file existence, size, package header type, HTTPS-only URLs, compatible asset type, and SHA-256 checksums when a checksum asset is published.
- Added pending update metadata JSON for verified downloads.
- Added technical failure details to Settings > Updates.
- Added ARK ASA settings page grouped navigation, server overview, section cards, boolean editors, and field validation indicators.

### Changed

- GitHub release selection now uses a shared compatible Windows asset matcher for check and download paths.
- Release packaging now names public Windows assets with an explicit `x64` architecture suffix.
- Update install action now confirms and launches the external installer as a separate process before closing the app.
- Automatic download settings now respect their dependencies.

### Fixed

- Fixed the updater flow that could stop after a failed download without a verified installer or usable install state.
- Rejected source archives, checksum files, debug/symbol packages, non-Windows assets, and incompatible architecture assets during updater asset selection.

## [3.0.8] - 2026-06-17

### Added

- Added a Settings regression test that verifies the Advanced page no longer exposes update commands or release status fields.
- Added persisted preferences for ask-before-download, update notifications, background downloads, opening release notes after update, verbose startup logging, service diagnostics, and log retention limits.

### Changed

- Rebuilt Settings > Updates as the authoritative client-update center with overview, actions, automatic update behavior, release channel, release information, update history, and advanced update source sections.
- Reworked Settings > Advanced into diagnostics, logging, troubleshooting, and About sections only.
- Improved settings navigation styling so selected, hover, keyboard focus, and disabled states are distinct.
- Updated update display fallbacks to use clearer professional text such as "Not checked yet" and "Not provided by release."

### Fixed

- Removed duplicated update buttons, release status, latest-version fields, download/install controls, and progress indicators from Settings > Advanced.
- Fixed automatic update download behavior to respect the ask-before-download preference.

## [3.0.3] - 2026-06-16

### Changed

- Check for Updates now shows a checking state, disables while running, and displays results in both Settings > Updates and Settings > Advanced.
- GitHub update checks now use `/releases/latest` for stable releases and log API URL, HTTP response, latest tag, version comparison, and asset selection.

### Fixed

- Fixed the Settings Check for Updates button appearing to do nothing when errors occurred or the request was in progress.
- Improved update error messages for missing releases, rate limits, network failures, invalid JSON, and version parse failures.
- Fixed version parsing for stable-suffix release tags such as `v3.0.1-stable`.

## [3.0.2] - 2026-06-16

### Added

- Import confirmation screen showing source, destination, server name, folder size, and detected game type.
- Copy-into-managed-folder import mode as the default server import behavior.
- Advanced link-existing-folder import option with warning text.
- Import copy progress, cancellation, disk-space checks, duplicate-safe destination naming, and `Logs/import.log` logging.

### Changed

- Imported servers now point to the copied managed server folder by default.

### Fixed

- Removed the generic RAM limit prompt from the Add/Edit Server wizard.

## [3.0.1] - 2026-06-16

### Added

- Professional GitHub Releases update system with Settings > Updates controls.
- Startup update notification banner.
- Updater logging to `updater.log`.
- Local update history storage.
- Safe update download folder with progress, speed display, and checksum verification when a release checksum file is available.
- GitHub owner/repository configuration in Settings.
- Developer update test tools for progress and failure states.
- Release and update system documentation.

### Changed

- Release metadata now points to `https://github.com/joshcarterky/Gamer-server-manager`.
- Release packaging regenerates installer, portable ZIP, Velopack feed files, checksums, and update metadata from `VERSION`.

### Fixed

- Fixed Settings page crash caused by a two-way binding on read-only `DownloadProgress`.

## [3.0.0] - 2026-06-16

### Added

- **Palworld Dedicated Server support** — full integration as a first-class game type alongside ARK: Survival Ascended.
  - `PalworldConfigDocument` parser correctly reads and writes the `OptionSettings=(...)` tuple format used by `PalWorldSettings.ini` — unknown and future settings (e.g. Palworld 1.0 keys) are preserved automatically.
  - Atomic config save with timestamped `.bak` backup before every write.
  - `PalworldSettingRegistry` — 70+ setting definitions across Server Identity, Network/Ports, Admin/Passwords, REST API, RCON, World Rates, Player Settings, Pal Settings, Base/Guild, Building/Decay, PvP, Death/Penalty, Crossplay, Performance, Randomizer, and Technology Restrictions categories.
  - `PalworldLaunchBuilder` — full launch argument support: `-port`, `-players`, `-publiclobby`, `-publicip`, `-publicport`, `-logformat`, `-useperfthreads`, `-NoAsyncLoadingThread`, `-UseMultithreadForDS`, `-NumberOfWorkerThreadsServer`, `-workshopdir`, `-NoMods`, and custom arguments.
  - `PalworldConfigService` — load/save with unknown-setting preservation; detects when `DefaultPalWorldSettings.ini` template is being edited instead of the active config and warns the user.
  - `PalworldValidator` — port conflict detection, range validation, admin password warnings, worker thread count safety check.
  - `PalworldBackupService` — full, save-only, config-only, and mods-only backup types with zip compression and metadata.
  - `PalworldPresetService` — 12 presets: Official-like, Small Friends, Casual PvE, Boosted PvE, Fast Leveling, Fast Capture, Fast Egg Hatching, High Gathering, PvP Test, Hardcore, Low-End PC, Palworld 1.0 Fresh Start.
  - `PalworldHealthService` — checks for missing executable, missing config, empty admin password, overdue backups, -NoMods/mods conflict.
  - `PalworldModManager` — reads/writes `PalModSettings.ini` (multi-line `ActiveModList=`), scans mod directories for `Info.json`, extracts `PackageName`, checks `InstallRules` for server compatibility.
  - `PalworldProvider` — enhanced with REST API port, performance mode, NoMods, log format, workshop directory, and full launch command building.
- **CurseForge Mod Browser** — browse and add ARK ASA mods from inside the app without copying IDs manually.
  - Search with sort by Popularity, Updated, Name, or Downloads.
  - Paginated results with Load More.
  - Selection cart — pick multiple mods and add them all at once.
  - API key configured in Settings → Integrations.

### Changed

- All assemblies versioned to `3.0.0.0`.
- `AppSettings.Version` updated to `3.0.0`.

### Fixed

- CurseForge browser no longer triggers API calls when changing sort order before a search has been performed.
- CurseForge browser state visibility bindings use global `BooleanToVisibilityConverter` to avoid silent binding failures.

## [0.1.0] - 2026-06-14

### Added

- Profile-backed Servers tab loaded from `Data/servers.json`.
- Multi-step Add Server and Edit Server workflow.
- Import server detection for ARK Survival Ascended, Minecraft Java, 7 Days to Die, Palworld, Rust, and Generic Server.
- Server row actions for Start, Stop, Restart, Edit, Delete, Open Folder, View Console, Backup Now, and Favorite.
- Real process start/stop/restart through tracked process IDs.
- CPU, RAM, uptime, start time, and stop time monitoring from tracked server processes.
- Steam-query style player count support for compatible dedicated servers.
- In-app console log viewer.
- Zip archive creation for manual Backup Now.
- Tests for provider definitions, repository save/load, import detection, and add/edit/delete persistence.

### Changed

- Servers tab now uses MVVM services and JSON persistence instead of static sample rows.
- Release preparation now includes automated test execution and versioned portable artifacts.

### Fixed

- Server filters and favorites persist correctly through `servers.json`.
- Add/Edit form validation reports missing required profile fields.

## [0.0.1] - 2026-06-13

### Added

- Open-source repository structure.
- Documentation for install, configuration, contribution, and release preparation.
- Git ignore rules for runtime data, server installs, backups, logs, and SteamCMD files.

### Changed

- Moved source projects under `/src`.
- Added shared version metadata through `Directory.Build.props`.

### Fixed

- Removed unused template `Class1.cs` files.
- Initial WPF application shell.
- Dashboard, Servers, and Settings views.
- Early game provider/profile model foundation.
- Portable Windows publish output.
