# ARK ASA Cluster Setup Guide

How to create and manage an ARK: Survival Ascended multi-map cluster using Nexus Server Manager.

---

## What Is a Cluster?

A cluster links multiple ARK ASA maps together so players can transfer characters, items, and dinos between them through the Obelisk (tribute terminal). Each map runs as a separate server process but shares a common **cluster directory** and **cluster ID**.

---

## Quick Start

### Step 1: Open the Cluster Tab

Navigate to **ARK ASA Settings → Cluster** tab.

### Step 2: Set Cluster Identity

| Field | Description | Example |
|---|---|---|
| **Cluster Name** | Display label for this cluster | `My Cluster` |
| **Cluster ID** | Shared ID used by every map (case-sensitive, no spaces) | `my-asa-cluster-01` |
| **Cluster Directory** | Shared folder all maps write transfer data to | `C:\Servers\ARK_ASA_Cluster` |

### Step 3: Add Your First Map

1. Select a map from the **Map** dropdown (e.g., The Island)
2. Set ports (game, query, RCON) — the manager auto-suggests non-conflicting ports
3. Click **Add Map**

Repeat for each additional map. The manager auto-increments ports.

### Step 4: Configure Transfer Rules

In the **Transfer Settings** section, decide what players can transfer:

| Setting | Recommended for PvE | Recommended for PvP |
|---|---|---|
| Allow Tribute Downloads | ✅ On | ✅ On |
| Prevent Download Survivors | ❌ Off | ❌ Off |
| Prevent Download Items | ❌ Off | ✅ On (first season) |
| Prevent Download Dinos | ❌ Off | ✅ On (first season) |
| No Transfer From Filtering | ❌ Off | ❌ Off |

### Step 5: Apply Cluster

Click **Apply Cluster**. This runs the full 5-step workflow:
1. Validates the cluster configuration
2. Saves cluster settings to every map profile
3. Syncs cluster-wide mods (if configured)
4. Creates the cluster directory on disk
5. Refreshes the cluster status panel

### Step 6: Start the Cluster

Click **Start All** to launch every map. The cluster is now live.

---

## Managing Cluster Mods

### Setting Cluster-Wide Mods

In the **Cluster Mods** field, enter a comma-separated list of CurseForge mod IDs that every map should run:

```
927083, 910300, 912345
```

Click **Sync Mods to Cluster** to push those IDs to every cluster map's profile.

### Checking Consistency

Click **Check Mod Consistency** to see if any maps have a different mod list. The panel shows which maps are missing which mods.

### Per-Map Mods

Use each map's individual **Mods** tab (in ARK ASA Settings for that server) to add map-specific mods on top of the cluster-wide list.

---

## Adding a New Map to an Existing Cluster

1. In the Cluster tab, select the new map from the dropdown
2. Adjust ports (auto-suggested based on existing maps)
3. Click **Add Map**
4. Click **Apply Cluster** to push the cluster ID and transfer rules to the new map
5. If you have cluster-wide mods, click **Sync Mods to Cluster**

---

## Removing a Map

Maps are not removed through the cluster view — they are managed in the main Servers list. Delete the server profile there, then click **Validate** in the cluster tab to refresh.

---

## Backup Strategy

The **Shared Backup** toggle enables automatic backups for every cluster map. Each map backs up:
- Its own `SavedArks/<AltSaveDirectoryName>/` directory
- Its `GameUserSettings.ini` and `Game.ini`
- The shared cluster directory (`ClusterDirOverride/`)

Backups are ZIP archives stored in `<InstallPath>/Backups/`.

---

## Cluster Directory

The cluster directory is where tribute transfers are staged. Every map server must have read/write access to it.

- **Recommended location**: A single folder outside any individual map's install path
- **Network shares**: Supported, but latency can cause transfer delays
- **Permissions**: The Windows user running the server processes must have full control

Example: `C:\Servers\ARK_ASA_Cluster`

Inside this folder, ARK creates: `<ClusterID>/` subdirectory automatically.

---

## Logs

Cluster operations are logged to:
- **Cluster log**: `Logs/ark-cluster.log` — start/stop/apply operations
- **Mods log**: `Logs/ark-mods.log` — mod sync operations

These files are appended and never auto-cleared.
