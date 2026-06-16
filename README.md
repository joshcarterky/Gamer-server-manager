# Nexus Server Manager

Nexus Server Manager is a Windows desktop application for managing dedicated game servers from one place. It is currently early-stage software and is being prepared for a future public GitHub release.

## Current Status

Version: `v0.1.0`

The app currently includes:

- WPF desktop shell
- Dashboard and settings screens
- Profile-backed Servers tab using `Data/servers.json`
- Add, edit, delete, import, favorite, search, and filter server profiles
- Real process start/stop/restart for configured server executables
- CPU/RAM/uptime monitoring for tracked server processes
- Console log viewer and manual zip backups
- Early provider/profile architecture
- Portable Windows publish support
- App icon and release packaging groundwork

This is still a pre-1.0 release. It is suitable for local testing and feedback, but some game-specific query/RCON/mod workflows are still incomplete.

## Repository Layout

```text
/
  src/                         Application source code
    GameServerManager.App/      WPF UI
    GameServerManager.Core/     Domain models and core services
    GameServerManager.GameProviders/
    GameServerManager.Infrastructure/
    GameServerManager.Services/
  assets/                      Icons, screenshots, release branding
  docs/                        User and developer documentation
  scripts/                     Build, release, and dev helper scripts
  tests/                       Future automated tests
  examples/                    Example configs and profiles
  releases/                    Local release staging, ignored by Git
  .github/workflows/           CI workflows
```

## Requirements

- Windows 10 or newer
- .NET 8 SDK for development
- .NET Desktop Runtime if running a framework-dependent build

## Build and Run From Source

```powershell
dotnet restore
dotnet build
dotnet run --project src\GameServerManager.App\GameServerManager.App.csproj
```

## Portable Release Build

```powershell
.\scripts\release\publish-win-x64.ps1
```

The portable output is written to:

```text
dist/GameServerManager-portable/
```

## Runtime Data

Nexus Server Manager separates app files from user-created server data.

Installed mode should store data in:

```text
%APPDATA%\NexusServerManager\
%USERPROFILE%\Documents\Nexus Server Manager\
```

Portable mode should store data beside the executable:

```text
data/
servers/
backups/
logs/
config/
tools/
```

See [docs/CONFIGURATION.md](docs/CONFIGURATION.md) for details.

## Supported Games Roadmap

Planned built-in providers:

- ARK Survival Ascended
- ARK Survival Evolved
- 7 Days to Die
- Palworld
- Minecraft Java
- Minecraft Bedrock
- Valheim
- Rust
- Conan Exiles
- Project Zomboid
- Satisfactory
- Factorio
- Generic executable server

## Safety

The app must never delete user server data during updates. Profiles, server installs, saves, backups, logs, configs, mods, and SteamCMD files are runtime data and must not be committed to Git.

## Documentation

- [Install Guide](docs/INSTALL.md)
- [Configuration Guide](docs/CONFIGURATION.md)
- [Contributing](CONTRIBUTING.md)
- [Release Checklist](RELEASE_CHECKLIST.md)
- [Changelog](CHANGELOG.md)

## License

License is not finalized yet. See [LICENSE](LICENSE).
