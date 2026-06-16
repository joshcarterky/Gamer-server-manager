# Release Notes

## v3.0.7

## Fixed

- **Settings > Advanced: Download Update button now visible** — when an update is available and you are on the Advanced settings page, the Download Update, Download and Install, Cancel Download, Install Update, and View Release buttons are now shown directly below the update status info box. Previously only the Updates page had these buttons, leaving Advanced with a status message that said "Click Download Update" but no button to click.
- **Update status text no longer references invisible buttons** — the status messages no longer say "Click Download Update to continue" or "Click Install Update to apply" since the action buttons are now self-explanatory.
- **Advanced page: download progress and ready-to-install banner** — the download progress bar, download detail text, green "ready to install" banner, and red "no installer found" banner are now shown on the Advanced page in addition to the Updates page.

---

## v3.0.6

## Added

- **ARK ASA Cluster: Apply Cluster workflow** — new "Apply Cluster" button runs a 5-step process: validate → sync cluster rules → sync mods → create cluster directory → refresh status.
- **ARK ASA Cluster: Sync Mods to Cluster** — enter comma-separated CurseForge mod IDs in the Cluster Mods field and push them to every map in one click.
- **ARK ASA Cluster: Mod Consistency Check** — detects which maps are missing mods that other maps have, with a per-issue breakdown.
- **ARK ASA Cluster: Dedicated Logging** — cluster and mod sync operations are now written to `Logs/ark-cluster.log` and `Logs/ark-mods.log`.
- **ARK ASA Cluster: IsBusy guard** — all cluster commands are disabled while an operation is in progress, preventing double-clicks and race conditions.
- **ARK ASA Cluster: Status color on map cards** — each map card shows green when running, grey when stopped.
- **Documentation** — new `docs/` folder with five guides: cluster research, mods research, cluster setup guide, mod manager guide, and cluster troubleshooting.

## Changed

- Cluster start/stop/restart/backup operations now log to `ark-cluster.log`.
- Adding a map to a cluster now logs to `ark-cluster.log`.
- Validation auto-runs a mod consistency check when two or more maps are present.

## Known Issues

- Code signing is prepared but not configured until a certificate is available.
