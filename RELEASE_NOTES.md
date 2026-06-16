# Nexus Server Manager v3.0.4

## Changed

- Minecraft Java server memory now defaults to **Game Default / Auto** — no `-Xmx` / `-Xms` arguments are added unless you choose Custom Limit in the server settings.
- Memory is now a per-server mode setting (Auto or Custom) instead of a global default allocation.
- The memory progress bar on server cards is hidden when no custom memory limit is configured.
- Memory usage text shows only current usage in Auto mode instead of "X / limit".
- Removed the global "Default RAM Allocation (MB)" field from Settings — replaced with a "Memory Mode: Game Default / Auto" note.

## Fixed

- Legacy `ramLimitMb` and `MemoryMb` profile settings are automatically migrated on first launch. Servers that use games which do not support app-level memory limits (e.g. ARK: Survival Ascended) have those settings removed cleanly.
- Existing Minecraft profiles with a fixed `MemoryMb` value are migrated to the new `CustomMemoryMb` setting — behaviour is preserved.

## Known Issues

- Code signing is prepared but not configured until a certificate is available.
