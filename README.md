# Nexus Server Manager

Nexus Server Manager is a professional Windows desktop app for installing, configuring, running, backing up, and updating dedicated game servers.

Current version: `v3.0.1`

## Screenshots

Screenshots will be added before the first public GitHub release.

## Features

- WPF desktop client for Windows.
- Dashboard, server inventory, settings, update, and diagnostics workflows.
- Profile-backed server storage with JSON persistence.
- Start, stop, restart, import, edit, favorite, filter, and backup server profiles.
- Process monitoring for CPU, RAM, uptime, and timestamps.
- Safe app update design that keeps user server data outside the install folder.
- Portable mode with `portable.flag`.
- Diagnostic report export with secret masking.

## Supported Games

- ARK: Survival Ascended
- Palworld
- Minecraft Java
- Minecraft Bedrock
- 7 Days to Die
- Valheim
- Rust
- Generic executable servers

## ARK ASA Support

ARK: Survival Ascended support includes SteamCMD app id `2430930`, launch command generation, cluster validation, INI preservation, backup helpers, health checks, and CurseForge mod browsing.

## Palworld Support

Palworld support includes `PalWorldSettings.ini` parsing/writing, launch arguments, REST/RCON settings, backups, presets, validation, health checks, and mod settings support.

## Install

Download the latest installer or portable ZIP from GitHub Releases.

- Installer: run `ServerManager-Setup-vX.Y.Z.exe`.
- Portable: extract `ServerManager-Portable-vX.Y.Z.zip` to a folder you control and keep `portable.flag` beside the executable.

## First-Time Setup

1. Start the app.
2. Open Settings.
3. Confirm the update channel and diagnostics preferences.
4. Add or import your first server from the Servers page.
5. Verify server paths, ports, passwords, and backup settings before starting a public server.

## Server Workflows

- Create a server from Servers > Add Server.
- Import an existing server folder from Servers > Import.
- Update a game server from its provider-specific tools or SteamCMD workflow.
- Back up a server before changing configs, mods, or versions.
- Restore by extracting a known-good backup into the original server folder while the server is stopped.

## App Updates

Open Settings > Updates to check GitHub Releases.

- Stable users receive stable releases only.
- Beta users can receive prereleases.
- Updates never delete saved servers, server installs, backups, logs, SteamCMD files, or app settings.
- Installer builds use Velopack for download/install/restart.
- Portable builds can check for updates and should be updated by replacing app files while preserving `Data`, `Servers`, and `portable.flag`.

See [UPDATE_SYSTEM.md](UPDATE_SYSTEM.md) for implementation details and [RELEASE.md](RELEASE.md) for publishing steps.

## Troubleshooting

- If update checks fail, verify internet access and the GitHub Releases page.
- If installation fails, close the app and retry from the installer.
- If Windows warns about an unknown publisher, the build is not code signed yet.
- If a server fails to start, verify the executable path, working directory, ports, and required game files.

## Report Bugs

Use the GitHub issue templates and attach a diagnostic report from Settings > Advanced. Do not paste passwords, API keys, RCON passwords, or private tokens.

## Development

```powershell
dotnet restore
dotnet build
dotnet run --project src\GameServerManager.App\GameServerManager.App.csproj
```

Create release artifacts:

```powershell
.\scripts\release\publish-win-x64.ps1
```

Outputs are written to `releases/v{version}/`.

## Roadmap

See [ROADMAP.md](ROADMAP.md).

## License

See [LICENSE](LICENSE).
