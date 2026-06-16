# ARK ASA Mod Manager Guide

How to manage mods for ARK: Survival Ascended servers using Nexus Server Manager.

---

## Overview

The Mods tab in ARK ASA Settings provides a full mod management interface:
- Add mods by CurseForge URL, project ID, or the built-in browser
- Reorder mods (load order matters)
- Mark a mod as the active map mod
- Import/export mod lists
- Validate mod configuration before starting

---

## Adding Mods

### By CurseForge URL

Paste a CurseForge URL into the **Add Mod** field:

```
https://www.curseforge.com/ark-survival-ascended/mods/dino-storage-v2
```

If a CurseForge API key is configured (Settings → Integrations), the app will automatically look up the project ID and show a preview panel. Click **Add Mod** to confirm.

If no API key is set, a manual project ID entry field appears. Find the project ID on the CurseForge page and enter it manually.

### By Project ID

Enter one or more numeric CurseForge project IDs, comma-separated:

```
927083, 910300
```

Click **Add Mod**. IDs must be numeric — slugs and URLs are not accepted in this field (use the URL path for URLs).

### CurseForge Browser

Click **Browse CurseForge** to open the built-in mod browser. Search by name, sort by downloads or date, and add multiple mods at once.

**Requires** a CurseForge API key configured in Settings → Integrations.

---

## Load Order

Mods are loaded in the order listed. The order matters when:
- Two mods modify the same blueprint or config
- A mod depends on another mod's content

Use the **↑** and **↓** buttons to reorder mods. The **Sort by Name** and **Sort by Date Added** buttons provide quick orderings.

**Best practice**: framework mods first, content mods after, map mod last (or via Active Map Mod).

---

## Active Map Mod

A custom map mod must be designated separately so the server knows which mod provides the world:

1. Ensure the map mod's project ID is in the mod list
2. Click **Set as Map Mod** on that entry
3. The mod's ID will appear in the **Active Map Mod** field
4. Update the server's `MapName` to match the mod's internal map name

The Active Map Mod ID is written to `GameUserSettings.ini`:
```ini
[ServerSettings]
ActiveMapMod=912345
```

---

## Cluster-Wide vs Per-Server Mods

The **Cluster Wide** toggle on each mod entry marks it for cluster-wide sync. When you use **Sync Mods to Cluster** in the Cluster tab, these mods are pushed to all cluster maps.

Mods without this flag are considered per-server only.

---

## Import and Export

### Import from Clipboard

Paste a mod ID list from another source:
- Raw IDs: `927083,910300,912345`
- With prefix: `ActiveMods=927083,910300,912345`
- With `-mods=` prefix: `-mods=927083,910300`

Click **Import from Clipboard**. Duplicates are skipped.

### Export to Clipboard

Click **Export to Clipboard** to copy the enabled mod IDs in comma-separated format, ready to paste into another server's launch args or share with others.

### Export to File

Click **Export to File** to save a `.txt` file with full mod details (IDs, enabled state, map mod flag, cluster-wide flag, names, and notes).

---

## Validation

The mod manager validates:
- **Duplicate IDs**: warns and marks the duplicate entries
- **Non-numeric IDs**: marks as invalid (ARK only accepts numeric CurseForge IDs)
- **Missing map mod**: warns if the Active Map Mod ID isn't in the mod list
- **Empty IDs**: warns about blank entries

Fix all errors before saving. Warnings do not block saving but should be reviewed.

---

## Mod Metadata

The app stores additional metadata per mod that is NOT written to the ARK INI files:
- **Name**: display name from CurseForge
- **Author**: mod author from CurseForge
- **Summary**: short description from CurseForge
- **CurseForge URL**: source page
- **Date Added**: when you added the mod

This metadata helps identify mods without needing to re-look them up.

---

## What Gets Written to INI

When you **Save** the server settings, the app writes:

```ini
[ServerSettings]
ActiveMods=927083,910300,912345
ActiveMapMod=912345      ← only if set
```

And the launch command includes:
```
-mods=927083,910300,912345
```

Only **enabled** mods are written. Disabled mods remain in the list but are excluded from the launch command and INI.

---

## Mod Notes

Each mod entry has a **Notes** field for personal reminders, such as:
- Why this mod was added
- Known conflicts
- Version information
- Whether a restart is required after updates

Notes are stored in the server profile and not written to any ARK files.
