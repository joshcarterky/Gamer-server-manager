# Installation Guide

Nexus Server Manager supports two installation modes.

## Portable Mode

Portable mode keeps application data beside the executable.

1. Download the portable zip from a GitHub release.
2. Extract it to a folder you control, such as:

   ```text
   C:\Games\NexusServerManager\
   ```

3. Run:

   ```text
   NexusServerManager.exe
   ```

4. The app creates runtime folders beside the executable when needed.

Portable mode layout:

```text
NexusServerManager.exe
data/
servers/
backups/
logs/
config/
tools/
temp/
```

## Installed Mode

Installed mode should keep application files separate from user data.

Application files:

```text
C:\Program Files\Nexus Server Manager\
```

User configuration:

```text
%APPDATA%\NexusServerManager\
```

Default server storage:

```text
%USERPROFILE%\Documents\Nexus Server Manager\Servers\
```

## Running From Source

Install the .NET 8 SDK, then run:

```powershell
dotnet restore
dotnet build
dotnet run --project src\GameServerManager.App\GameServerManager.App.csproj
```

## Troubleshooting

- If the app does not start, run it from PowerShell to see startup errors.
- If a server does not start, verify the executable path and working directory.
- If SteamCMD fails, check network access and write permissions.
- If ports conflict, change the profile ports before starting the server.
