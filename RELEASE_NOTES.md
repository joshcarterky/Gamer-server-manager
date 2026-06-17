# Release Notes

## v3.1.1

## Added

- **ARK ASA configuration synchronization state** - visual settings now load from the real `GameUserSettings.ini` and `Game.ini` files for the selected server before controls are created.
- **Raw editor synchronization** - raw INI edits now parse back into the same pending state used by the visual editor.
- **External file detection** - changes to the selected server's INI files are detected and either reloaded or reported when unsaved changes exist.
- **Regression coverage** - tests now verify disk-to-visual sync, visual-to-disk saves, raw editor sync, duplicate scalar cleanup, and repeated entry preservation.

## Changed

- Visual setting changes now update pending raw INI previews.
- Save now rereads and verifies written ARK keys before reporting success.
- Decimal and integer ARK values are serialized using invariant culture.

## Fixed

- Fixed visual ARK settings showing values that did not match the real INI files.
- Fixed raw INI text and visual controls acting like separate unsynchronized states.
- Fixed GameUserSettings.ini settings and Game.ini settings being vulnerable to stale profile/default values during load.

---

## v3.1.0

## Added

- **ARK ASA selected-category content host** - Overview, Health and Validation, Install / Update, Startup, normal settings categories, Mods, Cluster, and Raw INI Editor now render through explicit selected-category ownership.
- **Dedicated Raw INI Editor** - raw `GameUserSettings.ini`, `Game.ini`, launch arguments, generated configuration, and file comparison now live under Raw INI Editor tabs.
- **Regression coverage** - tests now guard against the technical panels returning as global content.

## Changed

- SteamCMD details now live under Install / Update behind a collapsed **Technical Command Preview** section.
- Generated launch command details now live under Startup behind a collapsed **View Generated Command** section.
- Current-vs-pending configuration diff now lives under Health and Validation.
- Basic and Advanced mode now use one mutually exclusive selector.
- The navigation count says "available settings" until search is active.

## Fixed

- Fixed the ARK ASA bug where SteamCMD, launch command, configuration diff, and full INI panels appeared under every category.
- Sensitive ARK values are masked in previews and raw editor display by default, and raw values are re-masked when leaving Raw INI Editor.

---

## v3.0.9

## Added

- **Professional updater download workflow** - downloads are now staged per target version using `.partial` files, then verified before the installer becomes available.
- **Update verification metadata** - successful downloads write update metadata with package name, channel, size, hash, install status, and restart executable information.
- **Updater technical details** - failed downloads and verification failures now surface technical details in Settings > Updates instead of only showing a generic error.
- **ARK ASA settings redesign pass** - the ARK settings page now has grouped navigation, a server context header, overview cards, section-based settings, boolean editors, validation indicators, and a sticky save bar.

## Changed

- GitHub release asset selection now rejects source archives, checksum files, symbols, debug packages, non-Windows packages, and incompatible architectures.
- Release asset names now include `x64` for clearer update matching.
- Settings > Updates now transitions through Preparing Download, Downloading, Verifying, and Ready to Install states.
- The install action is now labeled **Install and Restart** and confirms the current version, target version, installer name, size, and channel before launching.

## Fixed

- Fixed the updater path that could leave users at "The update download failed" without a usable install step or technical cause.
- Fixed automatic-update dependency behavior so automatic downloads require automatic checks, and background downloads require automatic downloads.

---

## v3.0.8

## Changed

- **Settings > Updates is now the only client-update center** - update checks, latest version details, release channels, downloads, install actions, release notes, update history, logs, and GitHub repository source settings now live under Settings > Updates.
- **Settings > Advanced is now diagnostics-focused** - Advanced now contains diagnostics, logging, troubleshooting, and About information only. It no longer duplicates update buttons or latest-release status.
- **Update settings UI has been reorganized** - Updates now includes overview, actions, automatic update behavior, release channel, release information, update history, and advanced update source cards.
- **Settings navigation polish** - selected, hover, keyboard focus, and disabled states are visually distinct so only one category appears selected at a time.

## Fixed

- **Automatic download preference** - automatic download behavior now respects "Ask before downloading" instead of incorrectly using the install confirmation setting.
- **Update fallback text** - version, release date, and download size fields now use clearer text such as "Not checked yet" and "Not provided by release" instead of confusing unknown states.
- **Advanced update duplication regression coverage** - tests now verify that Advanced does not expose update commands or release status fields.

---

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
