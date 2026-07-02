# Release Notes

## v4.1.0

### Added — 7 Days to Die V3.0 management upgrade
- **Installed-version detection** — the settings page now shows the actual installed game version, build, and Steam branch, read from the server's own boot logs and Steam manifest (`appmanifest_294420.acf`) instead of assuming anything. Configs are classified as V2.x or V3.0 from their real shape (SandboxCode vs legacy gameplay properties).
- **Configuration drift analysis** — the active `serverconfig.xml` is compared against the app's schema. Properties the app doesn't know (added by newer game versions or mods) are no longer invisible: they appear in a new **Unrecognized** category with a generic editor and an "UNRECOGNIZED" badge, and are preserved byte-for-byte on save. Retired/legacy properties are reported separately instead of being lumped in.
- **V2 → V3 Migration Assistant** — when legacy V2 gameplay properties (which abort a V3 server's startup) are found in the config, a migration panel lists every one with its old value, the matching V3 sandbox option, and an EXACT / APPROX / UNSUPPORTED classification — approximate matches carry your old value verbatim, never silently rounded. One click backs up the file, removes the legacy properties, and writes a JSON migration report next to the backup; **Undo Migration** restores the original byte-for-byte.
- **Sandbox Code panel upgrade** — Copy/Paste code buttons; a preset library (all 17 official V3 preset names as placeholders — codes are only stored once captured from your installed build, never hardcoded — plus custom presets you can save, apply, and delete); a decoded settings viewer fed by pasting the server's own `getsandboxoptions` (gso) console output; and a comparison baseline so a second gso import shows exactly which options changed. The code itself is still stored and applied exactly as entered — the game's encoding is proprietary, and the new codec architecture (`ISandboxCodec`) only decodes through implementations verified against a real build (none ship yet; the pipeline is proven by tests).
- **Versioned sandbox schema** — a built-in catalog of all 150 V3.0 sandbox options across the 8 official categories (Player, Entity, World, Resources, Crafting, Traders, Tasks, Miscellaneous), deliberately shipping names/categories only — allowed values and defaults are never invented, they come from a schema override file or gso capture.

### Fixed
- **Save can no longer overwrite external edits** — saving 7DtD settings now re-checks `serverconfig.xml` on disk first; if something else changed it since the app loaded it, the save is blocked with a conflict banner (Reload from File / Keep My Changes) instead of silently clobbering the newer file.

---

## v4.0.15

### Fixed
- **7 Days to Die still failed to start after v4.0.14** — `SaveGameFolder` is rejected as "Unknown config option" by the currently installed build. Removed from the settings panel and stripped from any config that already has it, same as the previous retired/invented properties.

---

## v4.0.14

### Fixed
- **7 Days to Die still failed to start after v4.0.13** — the "Web Control Panel" settings (`ControlPanelEnabled`, `ControlPanelPort`, `ControlPanelPassword`) were retired by the game back in Alpha 21, replaced entirely by the "Web Dashboard" settings the app already has. They're removed from the settings panel and stripped from any config that already has them. This round was cross-checked against two independent current (2026) sources covering the full property schema, rather than fixed one property at a time.

---

## v4.0.13

### Fixed
- **7 Days to Die still failed to start after v4.0.12** — this time the cause was different: `ServerAdminPassword`, one of the settings in the app's own 7DtD settings panel, was never a real serverconfig.xml property (verified against the current official V3 property reference). It's been removed from the settings panel and is now stripped from any config that already has it. Also added three more V3-removed legacy properties (`AirDropFrequency`, `AirDropMarker`, `QuestProgressionDailyLimit`) to the set that's never written back once a server has a Sandbox Code — they were encoded into SandboxCode in V3.0 and, like the other 26 already handled, will abort startup if written directly.

---

## v4.0.12

### Fixed
- **7 Days to Die still failed to start after v4.0.10** — that fix only excluded the `ipAddress` property that had been leaking into `serverconfig.xml`; the Add/Edit Server wizard actually stores nine of its own internal fields (description, tags, save/backup paths, CPU limit, auto-restart, RCON password, and more) into the same settings dictionary 7DtD's config writer reads from. Any one of them reaching the XML file aborts the game's startup. All of them are now excluded from `serverconfig.xml` and actively stripped from a config an older version already wrote them into — this closes the whole bug class instead of one property at a time. Confirmed ARK and Palworld are unaffected; they use their own isolated settings, not this shared dictionary.

---

## v4.0.11

### Fixed
- **Clicking Start silently did nothing when it failed** — Start/Stop/Restart failures were only ever reported in a tiny 11px status line at the bottom of the window, easy to miss entirely. A failed start now also pops up an error dialog with the actual reason, so a failure is never mistaken for the button doing nothing.

---

## v4.0.10

### Fixed
- **7 Days to Die server showed "Online" but was unreachable** — the app was writing its own internal `ipAddress` setting into `serverconfig.xml` as a `<property>`. 7 Days to Die aborts startup entirely on any config property it doesn't recognize, so the server process launched, failed to initialize, and quit within milliseconds — while the card kept showing it as running. Fixed on two levels: internal-only settings (`ipAddress`, `tags`, and import markers) are no longer written to `serverconfig.xml` and are actively stripped from any config file an older version already poisoned; and the status monitor no longer reports a server as Running/Online once its process has actually exited, for any reason (crash-on-boot, bad config, port conflict, etc.) — it now correctly falls back to Stopped so the card and the Start button reflect reality.

---

## v4.0.9

### Fixed
- **Server card stuck on "Stopping…" (and "Starting…")** — if a stop raced with the process already exiting (or a start/stop threw), the card could stay in the transient state forever, with the Stop button disabled. The status monitor now treats any non-running server as **Stopped** instead of echoing a stale transient state, and the stop routine always resolves cleanly out of "Stopping" and no longer errors on the already-exited race. Stopping also runs off the UI thread now, so the app stays responsive while a server shuts down.

---

## v4.0.8

### Fixed
- **Cascading "An existing connection was forcibly closed by the remote host" error dialogs when starting a server** — while a server was still booting, the status monitor's Steam query hit a not-yet-listening UDP port, which on Windows returns a connection-reset error. That error was wrapped in an `AggregateException` that slipped past the query's error handling and crashed the monitoring timer, spawning a new error dialog every few seconds. The query now surfaces and swallows the socket error correctly, and the monitor loop can no longer spam dialogs if a future refresh fails.

---

## v4.0.7

### Added
- **Backups page** — the Backups tab is now a real page. Pick any server to see its backup archives (date, size), and **Create**, **Restore**, **Delete**, or **Open Folder**. Restore is blocked while the server is running, always takes a safety backup of the current files first, and is protected against malicious archive paths (ZIP-slip).
- **Configuration drift detection** — when a server's config files are changed outside the app, the settings page now shows a banner offering **Reload from File** or **Keep My Changes** instead of silently overwriting. Wired for **7 Days to Die** (serverconfig.xml) and **ARK: Survival Ascended** (GameUserSettings.ini / Game.ini).

### Changed
- **Server card "More Options"** is now a working menu — **Copy Connection Address**, **Open Install Folder in Explorer**, and **Edit Profile** — replacing the previous placeholder.
- Backup archive filenames now include milliseconds, so a backup and a restore-time safety backup can never collide within the same second.

---

## v4.0.6

### Added
- **Game artwork on server tiles** — each server card now shows the official game key-art for its game (ARK, Palworld, Valheim, Rust, 7 Days to Die, Conan Exiles, Project Zomboid, Satisfactory, Factorio, and more). Art is streamed from Steam's public CDN on demand and cached; games without Steam art (Minecraft, generic) keep the colored initials tile.

### Fixed
- **7 Days to Die settings UI lag** — validation now debounces (400 ms) instead of running on every keystroke, and the settings list virtualizes so only on-screen cards are rendered. Typing and scrolling are now smooth.

---

## v4.0.5

### Added
- **7 Days to Die dedicated settings panel** — replacing the generic editor with a full professional UI: dark-themed sticky header with unsaved-change count badge, category sidebar with per-category error/warning badges, setting cards with descriptions, units, restart badges, and inline validation, a crossplay compatibility panel with requirement checklist and auto-fix button, a Sandbox Code (V3) panel with copy/paste workflow, and a detected-saves notice on the World & Map category.
- Settings now read from and write to **`serverconfig.xml`** directly (atomic save with `.bak` backup), in addition to `servers.json`. Server launch also syncs `serverconfig.xml` before starting the process.
- All 7DtD settings have descriptions, help text, units (for numeric fields), recommended values, and categories.

---

## v4.0.4

### Fixed
- Generic server settings editor: settings were not editable. Fixed dropdown bindings to use `SelectedValuePath`/`SelectedValue` (direct string mapping instead of object reference), switched text field bindings to `LostFocus` trigger to prevent feedback loop, and added error reporting on save.

---

## v4.0.3

### Added
- **Generic server settings editor** — clicking Settings on any non-ARK game (7 Days to Die, Minecraft, Palworld, Valheim, etc.) now opens a full categorized settings editor instead of a "not implemented" placeholder. Supports all control types: text, password, toggle, dropdown, number, and folder picker. Changes are saved back to the server profile with Save/Revert support.

---

## v4.0.2

### Fixed
- File Manager tab caused a cascade of error dialogs on open. A broken converter binding on the folder tree's indent rectangle used a `ResourceKey` instead of an `IValueConverter`, throwing on every tree node. Removed the redundant rectangle — the `ItemsPresenter` margin already handles indentation.

---

## v4.0.1

### Fixed
- Re-release to ensure all users receive the full v4.0.0 feature set (File Manager, Console, Scheduler, Monitoring, Deploy, 7 Days to Die integration). A CI race condition caused some installs to receive an incomplete build.

---

## v4.0.0

### Added
- **File Manager** — fully functional file browser for server install directories. Resizable folder tree, sortable file/folder table (Name, Type, Size, Modified), breadcrumb navigation, Back/Forward/Up/Home/Refresh controls, and search. File operations: create folder, create text file, rename, copy, cut, paste, move, delete (with confirmation), upload, download. Integrated text editor for `.ini`, `.json`, `.yaml`, `.xml`, `.txt`, `.cfg`, `.log`, and other config files with save, Save As, reload, and unsaved-change warnings. All paths are validated to stay inside the server's install directory — directory traversal via `..`, symlinks, or absolute paths outside the root is blocked.
- **Console view** — live log streaming and server command entry per server instance.
- **Scheduler view** — per-server scheduled tasks (restart, backup, update, etc.).
- **Monitoring view** — resource and health monitoring per server.
- **Deploy view** — deployment workflow UI.
- **System tray** — TrayService adds minimize-to-tray support.
- **7 Days to Die dedicated server support** — full first-class integration (Steam App ID 294420): 6 ports, 33 settings across 9 categories, headless launch flags, SteamCMD argument builder with branch selection and credential masking, `serverconfig.xml` parser/writer with atomic writes, V2→V3 migration protection, crossplay/EAC validation, five provider tests.

### Fixed
- Removed a stale test assertion for `GroupName="ArkMode"` no longer present in the ARK ASA settings XAML.

---

## v3.4.2

### Fixed
- **Fullscreen clipping** — maximizing the window cut off content on all four edges. With `WindowStyle="None"` and `WindowChrome.ResizeBorderThickness="6"`, Windows expands the window 6 px past each screen edge when maximized. `Shell` now overrides `OnStateChanged` to apply a matching 6 px margin to `ShellFrame` when maximized and removes it on restore.

---

## v3.4.1

### Fixed
- **ARK ASA — Max Players missing from settings UI** — `MaxPlayers` and `SessionName` had category `"Server Identity"` which matched no tab, making Max Players invisible in every settings tab. Both are now categorised under `Admin / Passwords` and appear in that tab under the Access Control section.

---

## v3.4.0

### Fixed
- **ARK ASA — MaxPlayers** — ASA uses `-WinLiveMaxPlayers=<n>` as a launch flag. The legacy URL-style `MaxPlayers=` query parameter (from ARK: Survival Evolved) was being emitted by both `ArkSurvivalAscendedProvider.BuildStartCommand` and `ArkAsaLaunchBuilder.Build`. Both are now corrected.
- **ARK ASA — ActiveMods** — `ArkAsaConfigService.SaveAsync` now skips `MaxPlayers` and `ActiveMods` when writing to `GameUserSettings.ini`. These are launch-only keys in ASA (the flag forms are `-WinLiveMaxPlayers=` and `-mods=`). Any old `MaxPlayers` entry already in the INI is actively removed on save.
- **ARK ASA — migration detection** — `ArkAsaConfigurationStateService.LoadAsync` now detects obsolete `MaxPlayers` and `ActiveMods` keys in an existing `GameUserSettings.ini` and surfaces warnings in `ArkServerConfigurationState.MigrationResult`. The ViewModel exposes `HasMigrationWarnings` and `MigrationWarningText` properties for the UI to display these warnings.
- **IniDocument** — added `RemoveKey(string section, string key)` to remove all occurrences of a key from a section, needed for pruning legacy keys on save.

### Added
- **ARK ASA Settings** — `OpenGameUserSettingsCommand` and `OpenGameIniCommand` on `ArkAsaSettingsViewModel` open the raw INI files in the system default editor.
- **ARK ASA tests** — twelve new targeted tests covering `-WinLiveMaxPlayers=` correctness, `MaxPlayers` exclusion from INI, `ActiveMods` exclusion from launch args, full INI round-trip, comment preservation, multi-server path isolation, invariant-culture decimal formatting, password redaction in launch preview, mod ordering in `-mods=`, migration detection, and minimal INI file creation.

---

## v3.3.9

### Fixed
- **ARK ASA Settings — crash on open** — clicking the Settings button on a server card caused an immediate crash. The Overview page's diagnostic strip used `<Run Text="{Binding ...}">` which defaults to TwoWay binding mode in WPF. The bound properties (`PathExistsText`, `PathWritableText`, `DiskSpaceText`, `ExecutableStatus`) are read-only, so WPF threw `InvalidOperationException` during layout. Fixed by adding `Mode=OneWay` to all four bindings.

---

## v3.3.8

### Changed
- **ARK ASA Settings — full UI redesign** — replaced the plain card layout with a premium dark-navy dashboard. New top header bar contains the ARK icon, title, server profile chip, settings search bar (Ctrl+K), Basic/Advanced segmented toggle, and Save Changes/Validate/Export/Reset Category action buttons. Left sidebar rebuilt with grouped navigation (OVERVIEW/SERVER/GAMEPLAY/MANAGEMENT/ADVANCED), Segoe MDL2 icon glyphs, and a live server status card at the bottom. Overview page now shows three status cards (Quick Status, Configuration Health, Ports) with real live data, a Quick Configuration card with server name/map/install path controls plus a diagnostic strip (path exists, writable, disk space, executable status), quick action buttons, and a three-column configuration status strip (Configuration, INI Sync, Launch Arguments). Unsaved changes now show as a slim amber banner under the header instead of a large bottom save bar.

---

## v3.3.7

### Fixed
- **ARK ASA Settings — Configuration Health warnings** — the warning count was including every setting that has a "dangerous" or "repeated line" label, even on a brand-new server where nothing had been changed. A fresh profile showed 57 warnings. The count now only includes a setting's warning if the user has actually changed that setting from its default value.

---

## v3.3.6

### Removed
- **ARK ASA Settings** — removed the "Install / Update" nav item and its tab content from the settings sidebar. Install and update actions are available from the server tile on the Servers page.

---

## v3.3.5

### Fixed
- **Install / Update log** — SteamCMD writes download progress via the Windows Console API (not stdout/stderr), so redirecting those streams only ever captured the initial banner. Now tails SteamCMD's own `logs/content_log.txt` file in real-time, which contains all app state changes, download percentages, and error messages. Also set SteamCMD's working directory to its own folder so it can locate its config and log files correctly.

---

## v3.3.4

### Fixed
- **Install / Update log** — SteamCMD uses carriage returns (`\r`) to overwrite progress lines in-place. The previous reader only fired on newlines (`\n`) so download percentages and update state were never captured. Switched to a raw character reader that handles both terminators, so all SteamCMD output now streams to the UI.

---

## v3.3.3

### Added
- **Installer** — setup now shows a "Choose Install Location" page powered by Inno Setup, so users can pick where the app installs instead of being silently placed in AppData.

### Fixed
- **Install / Update log** — SteamCMD's download progress, update percentages, and status messages now stream live into the install panel. Previously only the initial stdout banner was shown; all meaningful output (which SteamCMD writes to stderr) was being captured to the log file but never displayed.

---

## v3.3.2

## Fixed

- **ARK ASA Settings** — removed the redundant server context header (ARK badge, server name, status pills, Update Server / Open Config / Backup buttons) that was pinned at the top of the settings content area.

---

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
