# Release Guide

## 1. Update Version

Edit `VERSION`, then keep `CHANGELOG.md` and `RELEASE_NOTES.md` aligned with the same version.

Use tags like:

- `v1.0.0`
- `v1.0.1`
- `v1.1.0`
- `v1.0.0-beta.1`

## 2. Build Locally

```powershell
dotnet restore
dotnet build GameServerManager.sln --configuration Release
dotnet run --project tests\GameServerManager.ProviderTests\GameServerManager.ProviderTests.csproj --configuration Release
.\scripts\release\publish-win-x64.ps1
```

Artifacts are written to `releases/vX.Y.Z/`.

## 3. GitHub Release Assets

Upload these files when creating the GitHub Release:

- `ServerManager-Setup-vX.Y.Z.exe`
- `ServerManager-Portable-vX.Y.Z.zip`
- `NexusServerManager-X.Y.Z-stable-full.nupkg`
- `RELEASES-stable`
- `releases.stable.json`
- `assets.stable.json`
- `checksums.txt`
- `RELEASE_NOTES.md`
- `update.json`

## 4. Publish

1. Commit version, changelog, and release notes.
2. Push a tag like `vX.Y.Z`.
3. Confirm GitHub Actions builds successfully.
4. Download the installer and portable ZIP from GitHub.
5. Test install, startup, Settings > Updates, ARK ASA profile loading, and Palworld profile loading.

## 5. Code Signing

Windows will warn users about unknown publisher until signing is configured. See `docs/code-signing.md`.
