# Configuration and Runtime Data

Nexus Server Manager must keep app binaries, user data, downloaded servers, logs, backups, and temporary files separate.

## Installed Mode

Application files:

```text
C:\Program Files\Nexus Server Manager\
```

User app data:

```text
%APPDATA%\NexusServerManager\
  Config/
  Profiles/
  Settings/
  Logs/
  Cache/
```

User server data:

```text
%USERPROFILE%\Documents\Nexus Server Manager\
  Servers/
  Backups/
  Mods/
  Exports/
```

## Portable Mode

```text
NexusServerManager.exe
data/
  Config/
  Profiles/
  Settings/
  Logs/
servers/
backups/
mods/
tools/
  SteamCMD/
temp/
```

## Server Profiles

The current release stores server profiles in one JSON file:

```text
Data/servers.json
```

The app creates this file automatically when missing. It contains metadata only, such as game type, server name, ports, paths, favorite state, and timestamps.

Future versions may also support per-game profile files:

```text
Data/Profiles/{GameId}/{ProfileName}.json
```

Profiles should contain metadata only. Do not store save files or full server binaries in profile JSON.

## Logs

Application logs and server console logs should be written to:

```text
Data/Logs/
Data/Logs/Servers/
```

Logs should be rotated by size or age.

## Backups

Backups should be stored outside the app install folder:

```text
Backups/{GameId}/{ProfileName}/
```

Before overwriting any server config or save data, create a timestamped backup.

## SteamCMD

SteamCMD is downloaded at runtime and must not be committed to Git:

```text
tools/SteamCMD/
```

## Update Safety Rules

- Never delete profiles during updates.
- Never delete server install folders during updates.
- Never overwrite configs without making a backup.
- Never remove backups automatically unless the user enabled retention cleanup.
- Only clean folders known to be cache or temp folders.
- Use marker files for app-managed data roots before destructive cleanup.
