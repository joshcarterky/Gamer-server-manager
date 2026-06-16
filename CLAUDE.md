# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Commands

```bash
# Build entire solution
dotnet build GameServerManager.sln

# Run the WPF app (Debug)
dotnet run --project src/GameServerManager.App/GameServerManager.App.csproj

# Run all provider/integration tests
dotnet run --project tests/GameServerManager.ProviderTests/GameServerManager.ProviderTests.csproj

# Build for release (version injected from VERSION file via publish script)
.\scripts\release\publish-win-x64.ps1
```

The test project is a console program (`Program.cs`) that calls named `static` test functions directly — not xUnit/NUnit. Add new tests as `static void TestXxx()` or `static async Task TestXxxAsync()` functions and call them from `Main`.

## Release workflow

1. Edit `VERSION` (single file, e.g. `3.0.5`)
2. Update `RELEASE_NOTES.md`
3. Commit all changes, then `git tag v3.0.5 && git push origin master --tags`
4. GitHub Actions (`.github/workflows/release.yml`) triggers on the tag, runs `publish-win-x64.ps1`, and creates the GitHub Release with the installer, portable ZIP, and checksums

`Directory.Build.props` holds `VersionPrefix` for the build — the publish script overwrites it at publish time using the `VERSION` file value, so `Directory.Build.props` may lag behind `VERSION` between releases. `AppVersion.Current` reads the built assembly's `InformationalVersion` at runtime.

## Architecture

### Projects

| Project | Role |
|---|---|
| `GameServerManager.Core` | Models (`ServerProfile`, `AppSettings`, `GameDefinition`, enums) and legacy service interfaces |
| `GameServerManager.GameProviders` | `IGameServerProvider` + `GameServerProviderBase` + one concrete class per game |
| `GameServerManager.Services` | All runtime services (update system, server process, monitoring, backup, import, settings, paths) |
| `GameServerManager.App` | WPF UI — ViewModels + Views, no DI container |
| `GameServerManager.ProviderTests` | Console-based integration tests |

There is **no dependency injection container**. ViewModels instantiate services directly in their constructors.

### UI pattern

Pure MVVM. Each View binds to one ViewModel. `BaseViewModel` provides `INotifyPropertyChanged`. `RelayCommand` (defined in `DashboardViewModel.cs`, used project-wide) implements `ICommand` with an optional `canExecute` predicate. Converters (e.g. `BooleanToVisibilityConverter`) are declared in `App.xaml`.

### Data storage

`AppDataPaths` resolves all paths. Two modes detected at startup:
- **Installer** (default): `%LOCALAPPDATA%\Nexus Server Manager\`
- **Portable**: `<install dir>\Data\` when a `portable.flag` file exists next to the exe

Key paths: `ServersJsonPath` (`servers.json`), `SettingsDirectory\appsettings.json`, `LogsDirectory\updater.log`, `UpdateDownloadsDirectory`, `UpdateBackupsDirectory`.

Server profiles are stored in `servers.json` via `ServersJsonService` → `JsonServerProfileRepository`. Settings are stored in `appsettings.json` via `AppSettingsService`.

### Game provider system

Every supported game implements `IGameServerProvider` (via `GameServerProviderBase`). Each provider declares:
- `GameId`, `GameName`, `SupportedFeatures` (flags enum)
- `SettingsDefinitions` — list of `ServerSettingDefinition` records that drive the settings UI dynamically
- `BuildStartCommand(ServerProfile)` — returns the executable path + arguments

All providers are registered in `GameProviderRegistry.CreateDefault()`. Add new games by implementing `GameServerProviderBase` and adding to the registry.

Provider-specific settings use `ServerProfile.Settings` (a `Dictionary<string, string>`). Keys and defaults come from `SettingsDefinitions`. Use `MemorySettingsPolicy` for memory-related migration and start logging.

### Update system

Located in `Services/Updates/`. The flow is:

1. **`GitHubReleaseService.CheckLatestAsync`** — hits the GitHub API, parses releases, compares versions using `SemanticVersionInfo`, returns `UpdateCheckResult` with asset list
2. **`GitHubAssetDownloadService.DownloadBestWindowsAssetAsync`** — picks the best asset (Setup EXE > Installer EXE > Portable ZIP), streams it to `UpdateDownloadsDirectory`, verifies SHA256 checksum if a checksums file asset exists
3. **`SafeUpdateService`** — backs up settings before install; `GetInstallReadiness()` blocks install on portable builds
4. **Install** — if a downloaded `.exe` exists, launches it with `Process.Start` and calls `Application.Current.Shutdown()`; otherwise falls back to Velopack's `UpdateManager.ApplyUpdatesAndRestart`

The update state machine lives in `SettingsViewModel` — it owns `_lastUpdateResult`, `_downloadedUpdatePath`, download progress properties, and all update commands. `UpdateLogger` appends to `Logs/updater.log`. `UpdateHistoryService` persists the last 50 entries to `Settings/update-history.json`.

### Logging

No structured logging framework. Server process logs go to `Logs/Servers/<profileName>.log` (via `ServerProcessService`). Update logs go to `Logs/updater.log` (via `UpdateLogger`). Migration events go to `Logs/migration.log`.
