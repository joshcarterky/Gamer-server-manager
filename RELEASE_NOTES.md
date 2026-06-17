# Release Notes

## v3.3.1

## Changed

- **Server tile action bar** — replaced the white overflow/context menus with a permanent full-width action bar on every server card. All actions are now direct visible buttons: **Edit Profile**, **Console Log**, **Backup Now**, **Open Folder**, **Details**, **Settings** (MANAGEMENT group) and **More Options**, **Delete Server** (ADVANCED group). Each button has a coloured icon badge matching the mockup design.
- **Primary power controls** — the Start / Stop + dropdown (▾) and Install / Update buttons are now stacked vertically in the top-right of the card for a cleaner, more intentional hierarchy. Stop and Restart remain accessible via the power dropdown.
- **Delete Server styling** — red-accented button with trash icon, visually separated from management actions, still protected by confirmation dialog.

---

## v3.3.0

## Added

- **Install / Update workflow** — the "Install / Update" option in the server card's `⋯` menu now opens a dedicated overlay panel instead of doing nothing. The panel shows the server name, install path, and mode (Install vs Update detected from whether the server executable already exists).
- **SteamCMD integration** — installs SteamCMD automatically on first use, then runs `+app_update {appId} [validate] +quit` with the correct arguments per provider. Proper process management: stderr captured, exit code checked, entire process tree killed on cancel.
- **Install progress overlay** — live SteamCMD output streams into a scrolling console area inside the overlay. An indeterminate progress bar shows while SteamCMD runs.
- **Install / Update options** — "Validate files" checkbox (repairs corrupt installs) and "Restart server after successful update" checkbox in the confirmation step.
- **Install result panel** — after completion shows a colour-coded success (green) or failure (red) message, with an "Open Log" button to inspect the full SteamCMD output written to `Logs/steaminstall_*.txt`.
- **Running-server guard** — if the server is running when Install / Update is triggered, the user is prompted to stop it first; the update only proceeds after confirmation.
- **Per-server operation lock** — prevents launching a second install/update while one is already in progress for the same server.
- **`ServerInstallService`** — new service in `GameServerManager.Services` that owns the coordinator logic: validation, SteamCMD argument building, progress reporting, log writing, and structured result.
- **Install validation tests** — new `TestServerInstallServiceValidationAsync` in the provider test suite covers empty-path rejection, feature-flag checks, and ARK ASA App ID correctness.

## Fixed

- Fixed `SteamCMDService` always reporting `IsInstalled = false` even when `steamcmd.exe` was already on disk (constructor now checks for the file on startup).
- Fixed `SteamCMDService.InstallServerAsync` / `UpdateServerAsync` building commands that were missing `+app_update {AppId}` — SteamCMD would log in and quit without downloading anything.
- Fixed `CanStart` on server cards allowing the Start button while a server was in `Starting`, `Updating`, `Restarting`, or `Stopping` state, or while busy.

---

## v3.2.1

## Changed

- **Servers page redesigned** — replaced the plain server table with a professional card-based layout featuring summary stat cards (Total Servers, Online, Active Players, Needs Attention, Showing), a search/filter toolbar, and per-server cards with game identity tiles, colour-coded status badges, Players/CPU/RAM metric columns (showing `—` when the server is stopped), and an expandable details drawer.
- **Action hierarchy on server cards** — primary Start / Console button, icon buttons for Settings and Files, a `▾` power dropdown (Start / Stop / Restart), and a `⋯` overflow menu (Edit, Console log, Backup now, Install/Update, Open folder, Delete). Delete is now in the overflow menu only and still requires confirmation.
- **ARK cluster badge** — server cards for ARK: Survival Ascended show a cluster ID badge when clustering is enabled.
- **Game identity tiles** — each card shows coloured two-letter initials on a game-specific tinted background (orange for ARK, teal for Palworld, etc.).
- **Context menus fixed** — power and more dropdown menus now open anchored directly below their button.

## Fixed

- Fixed power (`▾`) and more (`⋯`) dropdown context menus appearing detached from the application window at the wrong screen position.

---

## v3.2.0

## Added

- **Per-map cluster settings** — the Cluster tab in ARK ASA settings now manages CrossARK identity (Cluster ID, Cluster Directory Override, Enable Cluster) directly on each individual server instead of through the global cluster dashboard.
- **Cluster status badge** — a live status pill on the Cluster tab shows Not configured / Invalid / Needs restart / Ready with colour coding.
- **Generate Cluster ID** — one-click button generates a unique `asa-<uuid>` cluster identifier and auto-enables clustering.
- **Browse / Create / Open folder commands** — inline directory picker, create-on-disk, and open-in-Explorer actions for the cluster transfer folder.
- **Cluster launch preview** — shows the exact `-clusterid=` and `-ClusterDirOverride=` flags appended to the launch command when clustering is enabled.
- **Cluster transfer presets** (global cluster dashboard) — five one-click presets: Open Cluster, Character Only, No Downloads In, One Way Out, and Locked Map.
- **Per-map session name and alt-save-directory override** — optional fields on the Add Map form let you customise session name and save directory when adding a map to a cluster.
- **Member-level validation** — each map card in the cluster dashboard now shows per-port and per-name validation issues inline.
- **NoTransferFromFiltering default on** — new clusters default to NoTransferFromFiltering enabled for safer transfer behaviour.

## Changed

- Cluster settings keys migrated to `Cluster.Enabled`, `Cluster.Id`, and `Cluster.DirectoryOverride`; old `ClusterID` / `ClusterDirOverride` / `ClusterEnabled` keys are read as fallback so existing profiles continue to work.
- Missing Cluster ID or Cluster Directory Override when clustering is enabled is now an **error** rather than a warning.
- `AllowTributeDownloads` resolution now also checks `noTributeDownloads` / `NoTributeDownloads` to prevent conflicting flags.
- Cluster Directory Override path is validated for illegal characters before save.
- The Cluster tab description updated to reflect its per-server scope.

## Fixed

- Fixed the Cluster tab DataContext being bound to the nested `Cluster` sub-model instead of the page ViewModel, which caused binding failures for cluster commands and status properties.
- Fixed `-clusterid=` and `-ClusterDirOverride=` flags being appended to the launch command even when clustering was disabled.

---

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
