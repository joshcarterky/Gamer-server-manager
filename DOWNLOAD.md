# Download Nexus Server Manager

Download the latest version from GitHub Releases:

https://github.com/joshcarterky/Gamer-server-manager/releases/latest

## Recommended Download

Most users should download the Windows installer:

`NexusServerManager-Setup-vX.Y.Z.exe`

The installer is the easiest option. It installs the app, creates the normal Windows app layout, and works with the in-app update flow.

## Portable Download

Advanced users can download the portable ZIP:

`NexusServerManager-Portable-vX.Y.Z.zip`

Use the portable ZIP if you want to run the app from a folder without installing it. Extract the ZIP to a folder you control and keep `portable.flag` beside the executable.

## What Not To Download

Normal users should not download updater or developer files such as:

- `.nupkg` packages
- `RELEASES-stable`
- `assets.stable.json`
- `releases.stable.json`
- `update.json`
- checksum files unless you are verifying downloads manually
- source code ZIP or TAR.GZ files

These files are for the updater system, release automation, or developers.

## Updates

Installed builds can check GitHub Releases from Settings > Updates. The app downloads the best Windows installer asset and verifies it with the release checksum file when available.

Portable builds can check for updates, but portable replacement is manual: download the latest portable ZIP, extract it, and preserve your `Data`, `Servers`, and `portable.flag` files.

## Download Or Update Problems

If a download or update fails, open a GitHub issue and include:

- the version you are running
- the file you downloaded
- any error shown in Settings > Updates
- whether you are using the installer or portable ZIP

Do not include passwords, API keys, RCON passwords, or private tokens.
