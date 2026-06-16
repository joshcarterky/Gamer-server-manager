# Changelog

All notable changes to Nexus Server Manager will be documented in this file.

This project follows semantic versioning.

## [Unreleased]

### Added

### Changed

### Fixed

## [3.0.1] - 2026-06-16

### Added

- Professional GitHub Releases update system with Settings > Updates controls.
- Startup update notification banner.
- Updater logging to `updater.log`.
- Local update history storage.
- Safe update download folder with progress, speed display, and checksum verification when `checksums.txt` is available.
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
