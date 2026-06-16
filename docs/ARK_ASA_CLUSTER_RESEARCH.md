# ARK ASA Cluster Configuration Research

Research into ARK: Survival Ascended dedicated server cluster requirements and configuration.

---

## Cluster Requirements

A functional ARK ASA cluster requires every participating server to share:

| Requirement | Value |
|---|---|
| `-clusterid=` | Same string on every map (case-sensitive) |
| `-ClusterDirOverride=` | Same directory path accessible by every server process |
| `AltSaveDirectoryName` | **Unique** per map — prevents save data collisions |
| Ports | **All unique** across game, query, and RCON ports |

The cluster directory must exist on disk before any server starts. Each server writes its upload/download data (uploaded survivors, items, dinos) into this directory.

---

## Command-Line Architecture

ARK ASA uses a URL-style argument format:

```
ArkAscendedServer.exe MapName?key=value?key2=value2 -flag1 -flag2
```

- Arguments before the first `-` are **URL query parameters** (passed to the UE5 map URL)
- Arguments starting with `-` are **flags** passed to the engine

### Cluster-Specific Flags

| Flag | Required | Notes |
|---|---|---|
| `-clusterid=<ID>` | Yes | Shared across all maps, case-sensitive |
| `-ClusterDirOverride="<path>"` | Yes | Directory for cross-server transfers |
| `-NoTransferFromFiltering` | Optional | Prevents filtering from blocking transfers |

### Transfer Control Flags (GameUserSettings.ini `[ServerSettings]`)

| Setting | Default | Effect |
|---|---|---|
| `PreventDownloadSurvivors=False` | False | Block downloading characters |
| `PreventDownloadItems=False` | False | Block downloading items |
| `PreventDownloadDinos=False` | False | Block downloading dinos |
| `PreventUploadSurvivors=False` | False | Block uploading characters |
| `PreventUploadItems=False` | False | Block uploading items |
| `PreventUploadDinos=False` | False | Block uploading dinos |
| `NoTributeDownloads=False` | False | Disable all tribute downloads |
| `AllowForeignDinoDownloads` (CrossARK) | — | Allow non-native dinos |
| `MaxTributeDinos=20` | 20 | Max dinos in tribute |
| `MaxTributeItems=50` | 50 | Max items in tribute |

---

## Cluster Directory Structure

```
<ClusterDirOverride>/
    <ClusterID>/
        SteamPlayer_<steamid>/
            Profile          ← character data
        SteamItem_<hash>     ← uploaded items
        SteamDino_<hash>     ← uploaded dinos
```

ARK creates the cluster ID subdirectory automatically. You only need to create the parent (`ClusterDirOverride`) directory.

---

## Alt Save Directory Names

Every cluster map **must** have a unique `AltSaveDirectoryName`. This controls where save data is stored under `ShooterGame/Saved/SavedArks/`:

```
Servers/ARK_Island/ShooterGame/Saved/SavedArks/<AltSaveDirectoryName>/
```

Recommended naming: `Island`, `Scorched`, `Center`, `Aberration`, `Extinction`, `Astraeos`, `Ragnarok`, `Valguero`, `LostColony`.

---

## Port Requirements

Each server needs three unique ports:

| Port | Protocol | Default | Description |
|---|---|---|---|
| Game Port | UDP | 7777 | Game traffic |
| Query Port | UDP | 27015 | Steam server browser |
| RCON Port | TCP | 27020 | Remote admin console |

Increment each map's ports to avoid conflicts. Common convention: `+1` on game and query, `+1` on RCON.

Example for 3-map cluster:
- Island: Game=7777, Query=27015, RCON=27020
- Scorched: Game=7778, Query=27016, RCON=27021
- Center: Game=7779, Query=27017, RCON=27022

---

## INI Configuration

### GameUserSettings.ini `[ServerSettings]`

All cluster transfer flags and session settings go here.

```ini
[ServerSettings]
SessionName=My Cluster - The Island
ServerAdminPassword=secretadmin
MaxPlayers=70
RCONEnabled=True
RCONPort=27020
PreventDownloadSurvivors=False
PreventDownloadItems=False
PreventDownloadDinos=False
PreventUploadSurvivors=False
PreventUploadItems=False
PreventUploadDinos=False
```

### No Cluster-Specific Section in GameUserSettings.ini

Unlike ARK: Survival Evolved, ASA does NOT use a `[/Script/ShooterGame.ShooterGameMode]` section for cluster settings. All cluster behavior is driven by launch flags.

---

## Cluster Validation Checklist

Before starting a cluster:

1. ✅ All maps share the same `-clusterid=`
2. ✅ All maps share the same `-ClusterDirOverride=`
3. ✅ Cluster directory exists and all server processes can write to it
4. ✅ Each map has a unique `AltSaveDirectoryName`
5. ✅ No port conflicts across any map
6. ✅ All servers use the same `ServerAdminPassword` (required for consistent RCON)
7. ✅ Each map's server executable exists at its install path

---

## Known Behaviors and Gotchas

- **ClusterID is case-sensitive.** `MyCluster` and `mycluster` are treated as different clusters.
- **Cluster directory must be accessible** from each server's working user account. Network paths work but require consistent mount points.
- **Transfer cooldowns** can be set via `MinimumDinoReuploadInterval` in `[ServerSettings]`.
- **Server must be running** for players to download from its tribute box — the cluster directory is shared, but the destination server must accept the connection.
- **Mods do not transfer** between servers automatically. Mods must be loaded on every map where players will use modded items/dinos.
- **SavedArks path** inside a cluster still uses the map's `AltSaveDirectoryName`, not the cluster ID. Backups should cover each map's own `SavedArks` directory.

---

## Sources

- ARK Wiki: Server Configuration — https://ark.wiki.gg/wiki/Server_configuration
- ARK Wiki: Dedicated Server Setup — https://ark.wiki.gg/wiki/Dedicated_server_setup
- ARK Fandom: ARK Cluster Setup — community guides
- ARK Official Discord: #server-setup channel
