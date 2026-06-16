# Contributing

Thanks for helping improve Nexus Server Manager.

## Development Setup

Requirements:

- Windows 10 or newer
- .NET 8 SDK
- Visual Studio 2022 or another C# editor

Build:

```powershell
dotnet restore
dotnet build
```

Run:

```powershell
dotnet run --project src\GameServerManager.App\GameServerManager.App.csproj
```

## Project Layout

```text
src/GameServerManager.App              WPF UI and MVVM view models
src/GameServerManager.Core             Models and core contracts
src/GameServerManager.GameProviders    Game provider definitions
src/GameServerManager.Infrastructure   Persistence and platform-specific adapters
src/GameServerManager.Services         Application orchestration services
```

## Code Guidelines

- Keep UI logic in view models where possible.
- Do not put server lifecycle logic in WPF code-behind.
- Keep game-specific behavior in provider classes.
- Keep user data paths configurable.
- Do not hardcode private local paths.
- Do not commit downloaded server files, saves, logs, backups, or secrets.

## Adding a Game Provider

1. Add a provider class under `GameServerManager.GameProviders`.
2. Define game ID, name, ports, install folders, config folder, saves folder, logs folder, and features.
3. Implement start command generation.
4. Add profile/template docs.
5. Add tests when the test project exists.

## Pull Request Checklist

- Builds locally.
- No generated files committed.
- No user data committed.
- Docs updated for user-facing changes.
- Changelog updated for notable changes.
- Screenshots included for major UI changes.
