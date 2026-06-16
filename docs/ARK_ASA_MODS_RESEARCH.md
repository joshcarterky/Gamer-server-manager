# ARK ASA Mod System Research

Research into ARK: Survival Ascended CurseForge mod loading and management for dedicated servers.

---

## Mod System Overview

ARK: Survival Ascended uses **CurseForge** as its primary mod distribution platform, replacing the Steam Workshop used by ARK: Survival Evolved. Mods are identified by their CurseForge **Project ID** (a numeric integer).

---

## Loading Mods on Dedicated Servers

### Command-Line Flag

```
-mods=<ID1>,<ID2>,<ID3>
```

- IDs are comma-separated, **no spaces**
- IDs are numeric CurseForge Project IDs (not slugs or URLs)
- **Load order** = order listed in `-mods=`; first ID = loaded first
- Mods not listed in `-mods=` will **not** be loaded even if downloaded

### GameUserSettings.ini Alternative

In addition to the command-line flag, the `ActiveMods` key in `[ServerSettings]` also controls which mods load:

```ini
[ServerSettings]
ActiveMods=927083,910300,912345
```

Both `-mods=` and `ActiveMods` are honored; the command-line flag takes precedence if both are set. Nexus Server Manager writes both for maximum compatibility.

### Map Mod (Custom Maps)

A custom map mod must be set separately:

```ini
[ServerSettings]
ActiveMapMod=912345
```

Or via command-line: the map name in the URL is the map's internal name from the mod (not a numeric ID).

---

## CurseForge Mod IDs

Every mod on CurseForge has:
- **Project ID**: numeric (e.g., `927083`) — used in `-mods=`
- **Slug**: URL-safe name (e.g., `dino-storage-v2`) — used in CurseForge URLs
- **URL format**: `https://www.curseforge.com/ark-survival-ascended/mods/<slug>`

To get a mod's project ID:
1. Visit the mod's CurseForge page
2. Look for the "Project ID" in the right sidebar
3. Or use the CurseForge API: `GET /v1/mods/search?gameId=<gameId>&slug=<slug>`

---

## CurseForge API Integration

The CurseForge API requires an API key. Key endpoints:

| Endpoint | Purpose |
|---|---|
| `GET /v1/mods/search?gameId=<id>&slug=<slug>` | Find mod by slug |
| `GET /v1/mods/<modId>` | Get mod details by project ID |
| `GET /v1/mods/search?gameId=<id>&searchFilter=<query>` | Search mods |

**ARK ASA CurseForge Game ID**: varies by region/environment. Check the CurseForge API or the game's CurseForge page to confirm. At time of writing: `83374`.

**Rate limits**: 10,000 requests/day on the free tier. Nexus Server Manager caches results and falls back gracefully when the API is unavailable.

---

## Mod Load Order

ARK ASA mods load in the order specified in `-mods=`. Load order matters when:
- Two mods override the same blueprint class
- A mod depends on content from another mod
- A map mod must load after its dependency mods

**Best practice**: Put framework/library mods first, content mods after, map mod last (or via `ActiveMapMod`).

---

## Cluster-Wide Mod Considerations

When running a cluster:
- **All maps should load the same core mods** — otherwise, items/dinos from one map may be unusable on another
- **Map-specific mods** (custom map mods) may differ per map
- **Framework mods** (e.g., ArkShop, HUD mods) should be identical across all cluster maps
- Players will see "corrupted" items if they transfer an item from a map that has a mod the destination map doesn't

### Recommended Approach

1. Maintain a **cluster-wide mod list** that every map loads
2. Add **map-specific mods** on top of the base list
3. Keep the `-mods=` argument in the same order on all maps for predictable override behavior
4. After changing the mod list, restart all servers in the cluster

---

## Mod Directories on Server

Downloaded mods are cached in:
```
<InstallPath>/ShooterGame/Binaries/Win64/ShooterGame/Mods/<ProjectID>/
```

The server downloads mods from CurseForge automatically when they appear in `-mods=`, as long as:
- The server process has internet access
- The mod is available on CurseForge for the current version

---

## Mod Validation

Before starting a server, check:
1. All mod IDs are **numeric** — not slugs or URLs
2. No **duplicate** mod IDs in the list
3. If a map mod is set, it exists in the mod list
4. No mod ID is set as both a regular mod and the map mod

---

## Custom Map Mods

Custom map mods have a special launch workflow:
1. The mod contains a custom map (UE5 World asset)
2. The `MapName` in the launch URL must match the internal map name from the mod
3. The `ActiveMapMod` setting in `[ServerSettings]` must be set to the mod's Project ID

Example:
```
ArkAscendedServer.exe MyCustomMap_WP?SessionName="My Server"... -mods=12345 ...
```
With:
```ini
[ServerSettings]
ActiveMapMod=12345
ActiveMods=12345
```

---

## Sources

- CurseForge API Docs: https://docs.curseforge.com/
- ARK Wiki: Mod support pages — https://ark.wiki.gg/wiki/Mods
- ARK ASA Server Setup Guide — community documentation
- CurseForge ARK ASA category: https://www.curseforge.com/ark-survival-ascended
