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
NexusServerManager-v0.1.0-win-x64-portable.zip
NexusServerManager-v0.1.0-win-x64-installer.exe
checksums.txt
release-notes.md
```

## Build Workflow

1. Restore dependencies.
2. Build Debug.
3. Run tests.
4. Build Release.
5. Publish portable win-x64 build.
6. Smoke-test executable.
7. Create zip.
8. Generate checksum.
9. Draft GitHub release.
