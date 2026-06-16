# Release Preparation Notes

Nexus Server Manager uses semantic versioning.

## Versioning

- `v0.x.y` - early development
- `v1.0.0` - first stable public release
- Patch versions fix bugs only
- Minor versions add features or supported games
- Major versions may include breaking profile/config changes

## Release Artifacts

Expected future release artifacts:

```text
NexusServerManager-Setup-vX.Y.Z.exe
NexusServerManager-Portable-vX.Y.Z.zip
NexusServerManager-Checksums-vX.Y.Z.txt
```

## Build Workflow

1. Restore dependencies.
2. Build Debug.
3. Run tests.
4. Build Release.
5. Publish win-x64 build.
6. Create installer.
7. Create portable ZIP.
8. Generate checksums.
9. Confirm GitHub release uploads only the clean public assets.
