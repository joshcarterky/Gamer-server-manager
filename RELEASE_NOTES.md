# Nexus Server Manager v3.0.3

## Changed

- Check for Updates now gives immediate visible feedback.
- The button changes to Checking... and disables while the update check is running.
- Settings > Advanced now shows update status, latest version, last checked time, and release channel.
- Update checks now log detailed GitHub API, response, version comparison, and asset-selection information.

## Fixed

- Fixed the Check for Updates button appearing to do nothing.
- Improved GitHub release, rate limit, network, JSON, and version parse error messages.
- Fixed stable-suffix version tags such as `v3.0.1-stable`.

## Known Issues

- Code signing is prepared but not configured until a certificate is available.
