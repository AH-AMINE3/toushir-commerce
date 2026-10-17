# Verified native previews

This directory is copied into the web service at `/downloads/`. The 17 October 2026 preview contains the verified, separately identified merchant and courier APKs for the public HTTPS staging API. Their hashes and public signing-certificate fingerprint are in `manifest.json`. Never substitute an emulator-only build for a public phone build.

After verifying the deployed HTTPS API, build each app using that exact origin and the private local signing key. Copy the resulting APKs as `toushir-merchant.apk` and `toushir-driver.apk`, and update `manifest.json`:

```json
{"merchant":{"apiOrigin":"https://verified-host.example","sha256":"actual-file-hash"},"driver":{"apiOrigin":"https://verified-host.example","sha256":"actual-file-hash"}}
```

`/api/distribution` exposes an app link only when its APK exists and its manifest HTTPS origin matches the current public API. Keystores, passwords and other private signing material never belong in this directory or Git. Publication remains separate from App Store/Play Store distribution.
