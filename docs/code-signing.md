# Code Signing

Windows may warn users about unknown publishers until the app executable and installer are signed with a trusted code signing certificate.

Certificates and private keys must never be committed to Git. Store signing material in GitHub Actions secrets or a dedicated signing service.

Signing should happen after publish/package generation and before checksums are created.

Future GitHub Actions secrets:

- `WINDOWS_SIGNING_CERTIFICATE`
- `WINDOWS_SIGNING_CERTIFICATE_PASSWORD`
- `WINDOWS_SIGNING_TIMESTAMP_URL`

TODO:

- Add a Windows code signing certificate.
- Sign the executable.
- Sign the installer.
- Verify signatures before creating the GitHub Release.
