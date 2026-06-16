# ARK ASA Cluster Troubleshooting Guide

Solutions for common ARK: Survival Ascended cluster issues.

---

## Players Cannot Transfer Between Maps

**Symptom**: Player opens Obelisk, uploads character/items, but cannot download on the other map.

### Check 1: Cluster ID Mismatch
Both maps must use exactly the same `-clusterid=` value. The ID is **case-sensitive**.

**In Nexus Server Manager**: Open Cluster tab → verify Cluster ID matches on every map. Click **Apply Cluster** to resync.

```
Map A: -clusterid=MyCluster  ✅
Map B: -clusterid=mycluster  ❌ (wrong case)
```

### Check 2: Cluster Directory Not Shared
Both maps must point to the **same physical directory**. If maps are on different machines, this path must be a shared network folder accessible to both.

**Check**: Open Cluster tab → verify Cluster Directory is identical and exists on disk.

### Check 3: Server Not Running
The destination server must be running to accept transfers. Players cannot download to an offline server.

### Check 4: Transfer Restrictions Enabled
Check these settings in the Cluster tab:
- `PreventDownloadSurvivors` / `PreventDownloadItems` / `PreventDownloadDinos`
- `NoTributeDownloads`

All should be `False` unless intentionally restricted. Click **Apply Cluster** after changing.

### Check 5: Ports Blocked
The destination server must be reachable. Verify the game port (UDP) is open in Windows Firewall and your router.

---

## "Cannot Connect to Server" After Adding New Map

**Symptom**: New cluster map added but clients cannot connect.

### Check: Port Conflict
Two servers cannot use the same UDP port. Check the cluster validation panel for port conflict errors.

**Fix**: In Cluster tab, the manager auto-assigns non-conflicting ports when adding maps. If you changed ports manually, verify each map has unique Game, Query, and RCON ports.

### Check: Firewall
Windows Firewall may block the new port. Allow the new game port (UDP) and query port (UDP) in Windows Defender Firewall.

---

## Mod Inconsistency Between Maps

**Symptom**: Player transfers a dino or item from Map A and it appears corrupted or missing on Map B.

**Cause**: Map B doesn't have the mod that creates or modifies that dino/item.

**Fix**:
1. Open **Cluster tab → Cluster Mods** field
2. Add the mod ID to the cluster-wide list
3. Click **Sync Mods to Cluster**
4. Restart all cluster maps
5. Click **Check Mod Consistency** to verify all maps agree

---

## Cluster Validation Shows "ClusterID Does Not Match"

**Symptom**: Validation panel shows an error about mismatched cluster IDs.

**Cause**: One or more maps were added or edited with a different cluster ID.

**Fix**:
1. Set the correct Cluster ID in the Cluster tab
2. Click **Apply Cluster** to push the ID to all maps

---

## Server Won't Start After Cluster Apply

**Symptom**: Server fails to launch after applying cluster settings.

### Check: Cluster Directory Path
If the path contains spaces, the `-ClusterDirOverride=` argument must be quoted. Nexus Server Manager handles this automatically. If you copied the launch command manually, ensure the path is quoted.

```
Correct:   -ClusterDirOverride="C:\Servers\My Cluster"
Incorrect: -ClusterDirOverride=C:\Servers\My Cluster
```

### Check: Invalid AltSaveDirectoryName
The `AltSaveDirectoryName` cannot contain special characters. Use only letters, numbers, and underscores.

### Check: Port Already in Use
Another process may be using the port. Click **Validate** to see port conflict errors. Use Task Manager → Details to find which process holds the port.

---

## Mods Not Loading After Sync

**Symptom**: Mods were synced to the cluster but aren't loading on some maps.

### Check: Server Restart Required
Mods only take effect after a **full server restart**. Stop and start the affected maps.

### Check: Invalid Mod IDs
Nexus Server Manager filters out non-numeric mod IDs during sync. Confirm all IDs in the Cluster Mods field are numeric CurseForge project IDs.

### Check: CurseForge Download Failed
The server downloads mods from CurseForge on startup. If download fails:
- Check server internet access
- Verify the mod ID is correct and the mod is still published on CurseForge
- Check the server console log for download errors

---

## Cluster Directory Fills Up Disk Space

**Symptom**: Disk space low; large files appearing in the cluster directory.

**Cause**: Uploaded survivors, items, and dinos accumulate in the cluster directory and are never automatically cleaned up by ARK.

**Fix**:
1. Have an admin use the in-game Obelisk to "download" and remove old uploads
2. Or, stop all servers and manually delete old files from `<ClusterDir>/<ClusterID>/`
3. Use a scheduled task to monitor disk usage

---

## Backup Fails for Cluster Maps

**Symptom**: Backup completes for some maps but fails or is incomplete for others.

### Check: Cluster Directory Included
When **Shared Backup** is enabled, the backup includes the cluster directory. This directory may be large if many transfers have accumulated. Verify the backup target has enough disk space.

### Check: Files In Use
ARK keeps save files open while running. Stop the server before running a manual backup, or use the **Backup Cluster** button which accepts the risk of in-use files.

---

## Logs Location

All cluster and mod operations are logged:

| Log File | Location |
|---|---|
| Cluster operations | `Logs/ark-cluster.log` |
| Mod sync operations | `Logs/ark-mods.log` |
| Individual server logs | `Logs/Servers/<ProfileName>.log` |
| Update logs | `Logs/updater.log` |

Check these logs first when diagnosing issues. They include timestamped records of every start/stop/apply/sync operation.
